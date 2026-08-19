using UnityEngine;

public class UI_EndTurnButton : MonoBehaviour
{
    public void OnEndTurnClicked()
    {
        TurnManager.Instance?.EndTurn();
    }
}
