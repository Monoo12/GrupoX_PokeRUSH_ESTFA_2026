// Cualquier cosa con la que el jugador pueda interactuar apretando la tecla de
// accion mirandola de frente. Lo busca PlayerController.Interact().
//
// Es una interfaz y no una clase base para que el equipo pueda colgarla de sus
// propios componentes (un NPC, una puerta con llave, una maquina) sin heredar
// de nada nuestro.
public interface IInteractable
{
    // Devuelve true si la interaccion se consumio. Sirve para que el jugador no
    // dispare dos cosas de un tecleo cuando hay varios colliders encimados.
    bool Interact(PlayerModel player, StoryModel story);
}
