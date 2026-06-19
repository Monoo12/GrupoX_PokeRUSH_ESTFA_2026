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
        PokenModel playerPoken = battleModel.Player.Team[0];
        PokenModel enemyPoken = battleModel.Enemy.Team[0];

        playerNameText.text = playerPoken.Definition.pokenName;
        playerHealthBar.SetHealth(playerPoken.CurrentHP, playerPoken.MaxHP);

        enemyNameText.text = enemyPoken.Definition.pokenName;
        enemyHealthBar.SetHealth(enemyPoken.CurrentHP, enemyPoken.MaxHP);

        turnText.text = battleModel.CurrentPhase == BattlePhase.PlayerTurn
            ? "Tu turno"
            : "Turno rival";
    }

    public void ShowMessage(string message)
    {
        messageText.text = message;
    }
}

//gameObject.SetActive(true) activa el GameObject completo (y todos sus hijos) en la escena. Si el HUD nunca se activa, todo lo que esté dentro queda invisible aunque el código que lo llena funcione perfecto — por eso Show()/Hide() son los primeros métodos que se llaman desde BattleController antes y después de la batalla.