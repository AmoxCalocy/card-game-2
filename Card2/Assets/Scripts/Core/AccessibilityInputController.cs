using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneJourney.Core
{
    /// <summary>
    /// A3-27：统一管理键盘焦点、Tab 循环、滚动跟随、快捷键提示和确认模态框。
    /// 鼠标仍走原有 Button 事件；键盘使用方向键/Tab、Enter/Space、Esc 与 H/F1。
    /// </summary>
    public sealed class AccessibilityInputController : MonoBehaviour
    {
        private sealed class FocusContext
        {
            public GameObject Root;
            public Selectable Preferred;
            public Selectable LastSelected;
            public Func<bool> Cancel;
            public string ContextHint;
            public bool AllowHelp;
            public bool PreferTopHint;
        }

        private static readonly Regex RichTextTag = new Regex("<.*?>", RegexOptions.Compiled);

        public static AccessibilityInputController Instance { get; private set; }

        [SerializeField] private AccessibilityOverlayView _view;

        private readonly List<FocusContext> _modalContexts = new List<FocusContext>();
        private readonly List<Selectable> _selectables = new List<Selectable>();
        private FocusContext _pageContext;
        private Coroutine _focusRoutine;
        private GameObject _lastSelectedObject;
        private bool _confirmationActive;
        private Action _confirmationAccepted;
        private Action _confirmationCancelled;

        public bool ConfirmationVisible => _confirmationActive;

        private FocusContext ActiveContext
        {
            get
            {
                for (int i = _modalContexts.Count - 1; i >= 0; i--)
                {
                    FocusContext context = _modalContexts[i];
                    if (context != null && context.Root != null && context.Root.activeInHierarchy)
                        return context;
                }

                return _pageContext != null && _pageContext.Root != null && _pageContext.Root.activeInHierarchy
                    ? _pageContext
                    : null;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Accessibility] 场景中存在多个 AccessibilityInputController", this);
                return;
            }

            Instance = this;
            EnsureView();
        }

        private void Start()
        {
            EnsureView();
            UpdateHint();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            FocusContext context = ActiveContext;
            if (context == null) return;

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                CycleFocus(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (IsEditingText())
                {
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                }
                else if (context.Cancel != null)
                {
                    context.Cancel.Invoke();
                }
            }

            if ((Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.F1))
                && context.AllowHelp && !IsEditingText() && !_confirmationActive)
            {
                TutorialCoordinator.Instance?.ShowHelp(HelpSection.Overview);
            }
        }

        private void LateUpdate()
        {
            EnsureView();
            if (_view != null && _view.transform.parent != null)
                _view.transform.SetAsLastSibling();

            FocusContext context = ActiveContext;
            if (context == null)
            {
                UpdateHint();
                return;
            }

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != _lastSelectedObject)
            {
                _lastSelectedObject = selected;
                Selectable selectable = selected != null ? selected.GetComponent<Selectable>() : null;
                if (selectable != null && IsInside(context.Root, selectable.gameObject))
                {
                    context.LastSelected = selectable;
                    EnsureVisible(selectable);
                }
                UpdateHint();
            }

            if (!IsValidSelection(context, selected))
                FocusFirstAvailable(context, false);
        }

        public void SetPageScope(GameObject root, Selectable preferred, Func<bool> cancel,
            string contextHint, bool allowHelp = true, bool preferTopHint = false)
        {
            _pageContext = new FocusContext
            {
                Root = root,
                Preferred = preferred,
                Cancel = cancel,
                ContextHint = contextHint,
                AllowHelp = allowHelp,
                PreferTopHint = preferTopHint
            };

            if (_modalContexts.Count == 0)
                ActivateContext(_pageContext, true);
        }

        public void RefreshPageScope(Selectable preferred = null)
        {
            if (_pageContext == null) return;
            if (preferred != null) _pageContext.Preferred = preferred;
            if (_modalContexts.Count == 0) ActivateContext(_pageContext, false);
        }

        public void PushModal(GameObject root, Selectable preferred, Func<bool> cancel,
            string contextHint, bool allowHelp = false)
        {
            if (root == null) return;

            FocusContext top = _modalContexts.Count > 0 ? _modalContexts[_modalContexts.Count - 1] : null;
            if (top != null && top.Root == root)
            {
                top.Preferred = preferred;
                top.Cancel = cancel;
                top.ContextHint = contextHint;
                top.AllowHelp = allowHelp;
                top.PreferTopHint = false;
                ActivateContext(top, true);
                return;
            }

            var context = new FocusContext
            {
                Root = root,
                Preferred = preferred,
                Cancel = cancel,
                ContextHint = contextHint,
                AllowHelp = allowHelp,
                PreferTopHint = false
            };
            _modalContexts.Add(context);
            ActivateContext(context, true);
        }

        public void RefreshModalScope(GameObject root, Selectable preferred = null)
        {
            for (int i = _modalContexts.Count - 1; i >= 0; i--)
            {
                if (_modalContexts[i].Root != root) continue;
                if (preferred != null) _modalContexts[i].Preferred = preferred;
                if (i == _modalContexts.Count - 1) ActivateContext(_modalContexts[i], false);
                return;
            }
        }

        public void PopModal(GameObject root)
        {
            if (root == null) return;
            bool removedTop = false;
            for (int i = _modalContexts.Count - 1; i >= 0; i--)
            {
                if (_modalContexts[i].Root != root) continue;
                removedTop = i == _modalContexts.Count - 1;
                _modalContexts.RemoveAt(i);
                break;
            }

            if (removedTop)
            {
                FocusContext context = ActiveContext;
                if (context != null) ActivateContext(context, false);
                else UpdateHint();
            }
        }

        public void ClearModals()
        {
            _modalContexts.Clear();
            _confirmationActive = false;
            _confirmationAccepted = null;
            _confirmationCancelled = null;
            if (_view != null) _view.HideConfirmation();
            if (_pageContext != null) ActivateContext(_pageContext, false);
            else UpdateHint();
        }

        /// <summary>显示安全确认框。返回 false 表示确认层不可用，调用方应直接执行或采用降级路径。</summary>
        public bool RequestConfirmation(string title, string detail, string confirmLabel,
            Action onConfirm, Action onCancel = null)
        {
            EnsureView();
            if (_view == null || _view.ConfirmationRoot == null) return false;

            _confirmationActive = true;
            _confirmationAccepted = onConfirm;
            _confirmationCancelled = onCancel;
            _view.ShowConfirmation(title, detail, confirmLabel, "取消",
                () => ResolveConfirmation(true), () => ResolveConfirmation(false));
            _view.transform.SetAsLastSibling();
            PushModal(_view.ConfirmationRoot, _view.CancelButton,
                () => { ResolveConfirmation(false); return true; },
                "默认焦点停在“取消”；确认前可核对最终成本与结果。", false);
            return true;
        }

        private void ResolveConfirmation(bool accepted)
        {
            if (!_confirmationActive) return;

            Action callback = accepted ? _confirmationAccepted : _confirmationCancelled;
            _confirmationActive = false;
            _confirmationAccepted = null;
            _confirmationCancelled = null;
            GameObject root = _view != null ? _view.ConfirmationRoot : null;
            if (_view != null) _view.HideConfirmation();
            if (root != null) PopModal(root);
            callback?.Invoke();
        }

        private void ActivateContext(FocusContext context, bool preferDefault)
        {
            if (context == null || context.Root == null || !context.Root.activeInHierarchy) return;
            RebuildSelectableList(context);

            Selectable target = preferDefault ? context.Preferred : context.LastSelected;
            if (!IsUsable(target, context.Root)) target = context.Preferred;
            if (!IsUsable(target, context.Root)) target = FirstUsable();
            ScheduleFocus(context, target);
            UpdateHint();
        }

        private void RebuildSelectableList(FocusContext context)
        {
            _selectables.Clear();
            if (context == null || context.Root == null) return;

            Selectable[] found = context.Root.GetComponentsInChildren<Selectable>(false);
            for (int i = 0; i < found.Length; i++)
            {
                Selectable selectable = found[i];
                if (!IsUsable(selectable, context.Root)) continue;

                Navigation navigation = selectable.navigation;
                if (navigation.mode == Navigation.Mode.None)
                    navigation.mode = Navigation.Mode.Automatic;
                navigation.wrapAround = true;
                selectable.navigation = navigation;

                var indicator = selectable.GetComponent<AccessibilityFocusIndicator>();
                if (indicator == null) indicator = selectable.gameObject.AddComponent<AccessibilityFocusIndicator>();
                indicator.Configure(selectable.targetGraphic);
                _selectables.Add(selectable);
            }
        }

        private void ScheduleFocus(FocusContext context, Selectable target)
        {
            if (_focusRoutine != null) StopCoroutine(_focusRoutine);
            _focusRoutine = StartCoroutine(FocusNextFrame(context, target));
        }

        private IEnumerator FocusNextFrame(FocusContext context, Selectable target)
        {
            yield return null;
            _focusRoutine = null;
            if (context != ActiveContext) yield break;

            RebuildSelectableList(context);
            if (!IsUsable(target, context.Root)) target = context.LastSelected;
            if (!IsUsable(target, context.Root)) target = context.Preferred;
            if (!IsUsable(target, context.Root)) target = FirstUsable();
            Focus(target);
        }

        private void FocusFirstAvailable(FocusContext context, bool preferDefault)
        {
            if (context == null) return;
            RebuildSelectableList(context);
            Selectable target = preferDefault ? context.Preferred : context.LastSelected;
            if (!IsUsable(target, context.Root)) target = context.Preferred;
            if (!IsUsable(target, context.Root)) target = FirstUsable();
            Focus(target);
        }

        private void Focus(Selectable selectable)
        {
            if (selectable == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            EnsureVisible(selectable);
            UpdateHint();
        }

        private void CycleFocus(int direction)
        {
            FocusContext context = ActiveContext;
            if (context == null) return;
            RebuildSelectableList(context);
            if (_selectables.Count == 0) return;

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int index = -1;
            for (int i = 0; i < _selectables.Count; i++)
            {
                if (_selectables[i] != null && _selectables[i].gameObject == selected)
                {
                    index = i;
                    break;
                }
            }

            int next = index < 0
                ? (direction > 0 ? 0 : _selectables.Count - 1)
                : (index + direction + _selectables.Count) % _selectables.Count;
            Focus(_selectables[next]);
        }

        private Selectable FirstUsable()
        {
            for (int i = 0; i < _selectables.Count; i++)
            {
                if (_selectables[i] != null) return _selectables[i];
            }
            return null;
        }

        private bool IsValidSelection(FocusContext context, GameObject selected)
        {
            if (context == null || selected == null || !IsInside(context.Root, selected)) return false;
            Selectable selectable = selected.GetComponent<Selectable>();
            return IsUsable(selectable, context.Root);
        }

        private static bool IsUsable(Selectable selectable, GameObject root)
        {
            return selectable != null && root != null && selectable.IsActive() && selectable.IsInteractable()
                && IsInside(root, selectable.gameObject);
        }

        private static bool IsInside(GameObject root, GameObject child)
        {
            return root != null && child != null
                && (root == child || child.transform.IsChildOf(root.transform));
        }

        private bool IsEditingText()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null) return false;
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            var legacy = selected.GetComponent<InputField>();
            if (legacy != null && legacy.isFocused) return true;
            var tmp = selected.GetComponent<TMP_InputField>();
            return tmp != null && tmp.isFocused;
        }

        private void UpdateHint()
        {
            EnsureView();
            if (_view == null) return;

            FocusContext context = ActiveContext;
            if (context == null)
            {
                _view.SetHintPlacement(false);
                _view.SetHints(null, "Tab / 方向键：切换焦点    Enter / 空格：确认");
                return;
            }

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            string focus = selected != null && IsInside(context.Root, selected)
                ? AccessibleName(selected)
                : AccessibleName(context.Preferred != null ? context.Preferred.gameObject : null);

            string shortcuts = "Tab / 方向键：切换焦点    Enter / 空格：确认";
            if (context.Cancel != null) shortcuts += "    Esc：取消 / 返回";
            if (context.AllowHelp) shortcuts += "    H / F1：规则说明";
            if (!string.IsNullOrEmpty(context.ContextHint)) shortcuts += "\n" + context.ContextHint;
            _view.SetHintPlacement(context.PreferTopHint);
            _view.SetHints(focus, shortcuts);
        }

        public static string AccessibleName(GameObject target)
        {
            if (target == null) return null;

            TMP_Text[] tmpTexts = target.GetComponentsInChildren<TMP_Text>(true);
            string value = PreferredText(tmpTexts);
            if (string.IsNullOrEmpty(value))
            {
                Text[] texts = target.GetComponentsInChildren<Text>(true);
                value = PreferredText(texts);
            }

            if (string.IsNullOrEmpty(value)) value = target.name;
            value = RichTextTag.Replace(value, string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            while (value.Contains("  ")) value = value.Replace("  ", " ");
            return value.Length > 48 ? value.Substring(0, 48) + "…" : value;
        }

        private static string PreferredText(TMP_Text[] texts)
        {
            if (texts == null) return null;
            string value = NamedText(texts, "Text", "Name", "Title", "Label");
            if (!string.IsNullOrEmpty(value)) return value;
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && !string.IsNullOrWhiteSpace(texts[i].text)) return texts[i].text;
            }
            return null;
        }

        private static string NamedText(TMP_Text[] texts, params string[] names)
        {
            for (int n = 0; n < names.Length; n++)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    if (texts[i] != null && texts[i].name == names[n] && !string.IsNullOrWhiteSpace(texts[i].text))
                        return texts[i].text;
                }
            }
            return null;
        }

        private static string PreferredText(Text[] texts)
        {
            if (texts == null) return null;
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == "Text" && !string.IsNullOrWhiteSpace(texts[i].text))
                    return texts[i].text;
            }
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && !string.IsNullOrWhiteSpace(texts[i].text)) return texts[i].text;
            }
            return null;
        }

        private static void EnsureVisible(Selectable selectable)
        {
            if (selectable == null) return;
            ScrollRect scroll = selectable.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null) return;

            RectTransform viewport = scroll.viewport != null ? scroll.viewport : scroll.GetComponent<RectTransform>();
            RectTransform target = selectable.transform as RectTransform;
            if (viewport == null || target == null || !target.IsChildOf(scroll.content)) return;

            Canvas.ForceUpdateCanvases();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            Rect viewRect = viewport.rect;
            Vector2 delta = Vector2.zero;

            if (scroll.vertical)
            {
                if (bounds.max.y > viewRect.yMax) delta.y -= bounds.max.y - viewRect.yMax;
                else if (bounds.min.y < viewRect.yMin) delta.y += viewRect.yMin - bounds.min.y;
            }

            if (scroll.horizontal)
            {
                if (bounds.min.x < viewRect.xMin) delta.x += viewRect.xMin - bounds.min.x;
                else if (bounds.max.x > viewRect.xMax) delta.x -= bounds.max.x - viewRect.xMax;
            }

            if (delta.sqrMagnitude > 0.01f)
            {
                scroll.content.anchoredPosition += delta;
                scroll.StopMovement();
                Canvas.ForceUpdateCanvases();
            }
        }

        private void EnsureView()
        {
            if (_view != null) return;
            Canvas canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null) return;
            _view = canvas.GetComponentInChildren<AccessibilityOverlayView>(true);
        }
    }

    /// <summary>在不覆盖原有配色的前提下，为当前 EventSystem 焦点追加高对比度描边。</summary>
    public sealed class AccessibilityFocusIndicator : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private Outline _outline;
        private bool _configured;

        public void Configure(Graphic targetGraphic)
        {
            if (_configured) return;
            _configured = true;
            if (targetGraphic == null) return;

            _outline = targetGraphic.gameObject.AddComponent<Outline>();
            _outline.effectColor = new Color(1f, 0.86f, 0.28f, 1f);
            _outline.effectDistance = new Vector2(5f, -5f);
            _outline.useGraphicAlpha = false;
            _outline.enabled = false;

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                _outline.enabled = true;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (_outline != null) _outline.enabled = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (_outline != null) _outline.enabled = false;
        }

        private void OnDisable()
        {
            if (_outline != null) _outline.enabled = false;
        }
    }
}
