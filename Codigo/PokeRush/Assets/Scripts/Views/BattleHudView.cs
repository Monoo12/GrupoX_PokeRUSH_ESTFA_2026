using UnityEngine;
using UnityEngine.UI;

public class BattleHudView : MonoBehaviour
{
    [Header("Jugador")]
    [SerializeField] private Text playerNameText;
    [SerializeField] private HealthBarView playerHealthBar;

    [Header("Enemigo")]
    [SerializeField] private Text enemyNameText;
    [SerializeField] private HealthBarView enemyHealthBar;

    [Header("Mensajes")]
    [SerializeField] private Text turnText;
    [SerializeField] private Text messageText;

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void UpdateHud(BattleModel battleModel)
    {
        PokenModel playerPoken = battleModel.ActivePlayerPoken;
        PokenModel enemyPoken = battleModel.ActiveEnemyPoken;

        if (playerNameText != null) playerNameText.text = playerPoken.Definition.pokenName;
        if (playerHealthBar != null) playerHealthBar.SetHealth(playerPoken.CurrentHP, playerPoken.MaxHP);

        if (enemyNameText != null) enemyNameText.text = enemyPoken.Definition.pokenName;
        if (enemyHealthBar != null) enemyHealthBar.SetHealth(enemyPoken.CurrentHP, enemyPoken.MaxHP);

        if (turnText != null)
            turnText.text = battleModel.CurrentPhase == BattlePhase.PlayerTurn
                ? "Tu turno"
                : "Turno rival";
    }

    public void ShowMessage(string message)
    {
        if (messageText != null) messageText.text = message;
        else Debug.Log($"[Batalla] {message}"); // sin HUD: al menos lo veo en consola
    }
}

//gameObject.SetActive(true) activa el GameObject completo (y todos sus hijos) en la escena. Si el HUD nunca se activa, todo lo que est� dentro queda invisible aunque el c�digo que lo llena funcione perfecto � por eso Show()/Hide() son los primeros m�todos que se llaman desde BattleController antes y despu�s de la batalla.