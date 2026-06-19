using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerInputController inputController;

    private PlayerModel _playerModel;
    private StoryModel _storyModel;

    public void Initialize(PlayerModel playerModel, StoryModel storyModel)
    {
        _playerModel = playerModel;
        _storyModel = storyModel;
    }

    public void Interact()
    {
        // detectar NPC cercano e iniciar dialogo o batalla — se implementa despues
    }

    public void OpenMenu()
    {
        // abrir menu de inventario o equipo — se implementa despues
    }

    public void SaveGame()
    {
        // delegar a SaveService — se implementa despues
    }

    public void Move(Vector2 direction, bool isRunning)
    {
        // mover el personaje en el mundo — se implementa despues
    }

    public void ChangePokenInBattle(PokenModel poken)
    {
        // cambiar pokemon activo durante batalla — se implementa despues
    }
}