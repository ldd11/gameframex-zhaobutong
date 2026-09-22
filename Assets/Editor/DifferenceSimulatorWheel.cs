#if ENABLE_UI_UGUI
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class DifferenceSimulatorWheel
{
    const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static EditorWindow window;
    static VisualElement view;
    static object main, touchInput;
    static PropertyInfo viewToScreen, currentScreen;
    static MethodInfo toTouch, onCutout;
    static readonly List<RaycastResult> hits = new List<RaycastResult>();

    static DifferenceSimulatorWheel() { EditorApplication.update += Attach; }

    static void Attach()
    {
        var candidate = EditorWindow.mouseOverWindow;
        if (!candidate || candidate.GetType().FullName != "UnityEditor.DeviceSimulation.SimulatorWindow") return;
        if (candidate == window && view?.panel != null) return;
        if (view != null) view.UnregisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
        window = candidate;
        // ponytail: Unity 2022.3 has no public Simulator wheel API; recheck these members when upgrading Unity.
        main = window.GetType().GetProperty("main", Public).GetValue(window);
        if (main == null) { view = null; return; }
        var ui = main.GetType().GetProperty("userInterface", Public).GetValue(main);
        view = (VisualElement)ui.GetType().GetProperty("DeviceView", Public).GetValue(ui);
        viewToScreen = view.GetType().GetProperty("ViewToScreen", Public);
        currentScreen = main.GetType().GetProperty("currentScreen", Public);
        touchInput = main.GetType().GetField("m_TouchInput", Private).GetValue(main);
        toTouch = touchInput.GetType().GetMethod("ScreenPixelToTouchCoordinate", Private);
        onCutout = touchInput.GetType().GetMethod("IsTouchOnCutout", Private);
        view.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
    }

    static void OnWheel(WheelEvent evt)
    {
        if (!EditorApplication.isPlaying || !EventSystem.current || evt.ctrlKey || evt.altKey || evt.commandKey) return;
        var raw = (Vector2)((Matrix4x4)viewToScreen.GetValue(view)).MultiplyPoint(evt.localMousePosition);
        var screen = currentScreen.GetValue(main);
        var width = (int)screen.GetType().GetField("width", Public).GetValue(screen);
        var height = (int)screen.GetType().GetField("height", Public).GetValue(screen);
        if (raw.x < 0 || raw.x > width || raw.y < 0 || raw.y > height) return;
        var point = (Vector2)toTouch.Invoke(touchInput, new object[] { raw });
        if ((bool)onCutout.Invoke(touchInput, new object[] { raw, point })) return;
        var data = new PointerEventData(EventSystem.current) { position = point, scrollDelta = -(Vector2)evt.delta / 3f };
        hits.Clear(); EventSystem.current.RaycastAll(data, hits);
        if (hits.Count == 0) return;
        data.pointerCurrentRaycast = hits[0];
        var handler = ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject);
        if (!handler) return;
        ExecuteEvents.ExecuteHierarchy(handler, data, ExecuteEvents.scrollHandler);
        evt.StopPropagation(); evt.PreventDefault();
    }
}
#endif
