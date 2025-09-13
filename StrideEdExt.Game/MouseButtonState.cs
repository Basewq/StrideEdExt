using Stride.Input;

namespace StrideEdExt;

public enum MouseButtonState
{
    Up,
    JustPressed,
    HeldDown,
    JustReleased
}

public static class MouseButtonStateExtensions
{
    public static bool IsDown(this MouseButtonState mouseButtonState)
    {
        switch (mouseButtonState)
        {
            case MouseButtonState.JustPressed:
            case MouseButtonState.HeldDown:
                return true;
            default:
                return false;
        }
    }

    public static MouseButtonState GetButtonState(InputManager inputManager, MouseButton mouseButton)
    {
        bool isLeftMousePressed = inputManager.IsMouseButtonPressed(mouseButton);
        bool isLeftMouseDown = inputManager.IsMouseButtonDown(mouseButton);
        bool isLeftMouseReleased = inputManager.IsMouseButtonReleased(mouseButton);
        var mouseButtonState = MouseButtonState.Up;
        if (isLeftMousePressed)
        {
            mouseButtonState = MouseButtonState.JustPressed;
        }
        else if (isLeftMouseReleased)
        {
            mouseButtonState = MouseButtonState.JustReleased;
        }
        else if (isLeftMouseDown)
        {
            mouseButtonState = MouseButtonState.HeldDown;
        }
        return mouseButtonState;
    }
}
