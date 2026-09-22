#if ENABLE_UI_UGUI
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hotfix.UI
{
    public sealed class DifferenceTabPress : MonoBehaviour, IPointerDownHandler
    {
        public void OnPointerDown(PointerEventData data)
        {
            var button = GetComponent<UnityEngine.UI.Button>();
            if (data.button != PointerEventData.InputButton.Left || !button.IsActive() || !button.IsInteractable()) return;
            // Navigation is reversible: respond on press, without replaying the click on release.
            data.eligibleForClick = false;
            button.onClick.Invoke();
        }
    }
}
#endif
