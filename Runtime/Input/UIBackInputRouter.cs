using NKStudio.UITKNavigation.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NKStudio.UITKNavigation.Input
{
    /// <summary>
    /// Provides UI Back Input Router functionality.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIBackInputRouter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("마우스 4번(뒤로) / 5번(앞으로) 버튼을 사용할지 여부입니다.")]
        private bool enableMouseButtons = true;

        [SerializeField]
        [Tooltip("Escape 키를 뒤로 가기로 사용할지 여부입니다.")]
        private bool enableEscape = true;

        [SerializeField]
        [Tooltip("Backspace 키를 뒤로 가기로 사용할지 여부입니다. 텍스트 입력 중에는 자동으로 무시됩니다.")]
        private bool enableBackspace = true;

        [SerializeField]
        [Min(0f)]
        [Tooltip("뒤로 가기 연타를 막는 최소 간격(초)입니다.")]
        private float backRateLimit = 0.2f;

        private float _lastBackTime = float.NegativeInfinity;

        private void Update()
        {
            if (UIInteractionGate.IsBlocked)
                return;

            Mouse mouse = enableMouseButtons ? Mouse.current : null;
            Keyboard keyboard = Keyboard.current;

            bool back = mouse != null && mouse.backButton.wasPressedThisFrame;
            bool forward = mouse != null && mouse.forwardButton.wasPressedThisFrame;

            if (keyboard != null)
            {
                if (enableEscape && keyboard.escapeKey.wasPressedThisFrame)
                    back = true;

                if (enableBackspace
                    && keyboard.backspaceKey.wasPressedThisFrame
                    && !UIInteractionGate.IsTextInputFocused)
                    back = true;
            }

            if (back && Time.unscaledTime - _lastBackTime >= backRateLimit)
            {
                _lastBackTime = Time.unscaledTime;
                UINavigationEvents.RequestBack();
            }

            if (forward)
                UINavigationEvents.RequestForward();
        }
    }
}
