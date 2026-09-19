using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace TheBlackBox.Checks;

/// <summary>
/// Mouse, keyboard and gamepad read together, with the previous frame kept for edge detection.
/// Same idea as the input tutorial, so a check does not care which device the player is on.
/// 
/// Hits our criteria
/// </summary>
/// <remarks>
/// The mouse only counts as pointing on frames it actually moved, otherwise a mouse sitting
/// on the desk would fight a keyboard player.
/// </remarks>
public class PlayerInput
{
    /// <summary>How far the stick has to go over to count as a step.</summary>
    private const float StickFlick = 0.5f;

    /// <summary>Below this the stick is resting, not being pushed.</summary>
    private const float Deadzone = 0.2f;

    private MouseState _mouse;
    private MouseState _priorMouse;
    private KeyboardState _keyboard;
    private KeyboardState _priorKeyboard;
    private GamePadState _pad;
    private GamePadState _priorPad;

    /// <summary>Reads everything and forgets the frame before.</summary>
    /// <remarks>Called when a check begins, so the click that started it does not also fire it.</remarks>
    public void Reset()
    {
        _mouse = _priorMouse = Mouse.GetState();
        _keyboard = _priorKeyboard = Keyboard.GetState();
        _pad = _priorPad = GamePad.GetState(PlayerIndex.One);
    }

    /// <summary>Reads all three devices. Once a frame, before anything is asked.</summary>
    public void Update()
    {
        _priorMouse = _mouse;
        _priorKeyboard = _keyboard;
        _priorPad = _pad;

        _mouse = Mouse.GetState();
        _keyboard = Keyboard.GetState();
        _pad = GamePad.GetState(PlayerIndex.One);
    }

    /// <summary>Where the mouse is, in screen pixels.</summary>
    public Vector2 MousePosition => new(_mouse.X, _mouse.Y);

    /// <summary>Whether the mouse moved since last frame.</summary>
    public bool MouseMoved => _mouse.X != _priorMouse.X || _mouse.Y != _priorMouse.Y;

    /// <summary>How hard the player is pushing on each axis, -1 to 1. Keys, stick and d-pad all add up.</summary>
    public Vector2 Push
    {
        get
        {
            float x = 0f, y = 0f;

            if (_keyboard.IsKeyDown(Keys.Left) || _keyboard.IsKeyDown(Keys.A)) x -= 1f;
            if (_keyboard.IsKeyDown(Keys.Right) || _keyboard.IsKeyDown(Keys.D)) x += 1f;
            if (_keyboard.IsKeyDown(Keys.Up) || _keyboard.IsKeyDown(Keys.W)) y -= 1f;
            if (_keyboard.IsKeyDown(Keys.Down) || _keyboard.IsKeyDown(Keys.S)) y += 1f;

            if (_pad.IsConnected)
            {
                Vector2 stick = _pad.ThumbSticks.Left;
                if (Math.Abs(stick.X) > Deadzone) x += stick.X;
                if (Math.Abs(stick.Y) > Deadzone) y -= stick.Y;     // the stick's up is positive
                if (_pad.DPad.Left == ButtonState.Pressed) x -= 1f;
                if (_pad.DPad.Right == ButtonState.Pressed) x += 1f;
                if (_pad.DPad.Up == ButtonState.Pressed) y -= 1f;
                if (_pad.DPad.Down == ButtonState.Pressed) y += 1f;
            }

            return new Vector2(Math.Clamp(x, -1f, 1f), Math.Clamp(y, -1f, 1f));
        }
    }

    /// <summary>Whether the player fired this frame: click, Space, Enter, or A. On the edge, so one press is one shot.</summary>
    public bool Fired =>
        (_mouse.LeftButton == ButtonState.Pressed && _priorMouse.LeftButton == ButtonState.Released)
        || Pressed(Keys.Space) || Pressed(Keys.Enter)
        || (_pad.IsConnected && _pad.Buttons.A == ButtonState.Pressed && _priorPad.Buttons.A == ButtonState.Released);

    /// <summary>Whether the mouse was clicked this frame.</summary>
    public bool Clicked =>
        _mouse.LeftButton == ButtonState.Pressed && _priorMouse.LeftButton == ButtonState.Released;

    /// <summary>Whether the player stepped left this frame: arrow, A, d-pad, or the stick flicked.</summary>
    public bool SteppedLeft =>
        Pressed(Keys.Left) || Pressed(Keys.A)
        || (_pad.IsConnected && (_pad.DPad.Left == ButtonState.Pressed && _priorPad.DPad.Left == ButtonState.Released
            || _pad.ThumbSticks.Left.X < -StickFlick && _priorPad.ThumbSticks.Left.X >= -StickFlick));

    /// <summary>Whether the player stepped right this frame. See <see cref="SteppedLeft"/>.</summary>
    public bool SteppedRight =>
        Pressed(Keys.Right) || Pressed(Keys.D)
        || (_pad.IsConnected && (_pad.DPad.Right == ButtonState.Pressed && _priorPad.DPad.Right == ButtonState.Released
            || _pad.ThumbSticks.Left.X > StickFlick && _priorPad.ThumbSticks.Left.X <= StickFlick));

    /// <summary>Which of the number keys 1 to 3 went down this frame, from 0, or -1 if none did.</summary>
    public int NumberPressed
    {
        get
        {
            if (Pressed(Keys.D1) || Pressed(Keys.NumPad1)) return 0;
            if (Pressed(Keys.D2) || Pressed(Keys.NumPad2)) return 1;
            if (Pressed(Keys.D3) || Pressed(Keys.NumPad3)) return 2;
            return -1;
        }
    }

    /// <summary>Whether a key went down this frame.</summary>
    /// <param name="key">The key.</param>
    private bool Pressed(Keys key) => _keyboard.IsKeyDown(key) && _priorKeyboard.IsKeyUp(key);
}
