// Generated with Antigravity by jjmil on 2026-03-29.
// Interface for components that need to respond to
// BoardComponent selection state changes.

/// <summary>
/// Implement on a MonoBehaviour that sits on the same
/// GameObject as a <see cref="BoardComponent"/> to
/// receive selection/deselection notifications.
/// </summary>
public interface IBoardComponentSelectionListener
{
    void OnBoardComponentSelected();

    void OnBoardComponentDeselected();

    void OnBoardComponentPrewarmed();
}
