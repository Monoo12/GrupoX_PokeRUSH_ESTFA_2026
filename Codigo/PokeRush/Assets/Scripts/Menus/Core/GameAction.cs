// Acciones logicas del juego. NINGUN menu mira teclas: todos preguntan por la
// accion, y GameInputService traduce accion -> tecla segun la configuracion
// guardada. Cambiar el binding en Opciones se ve reflejado en todos lados sin
// tocar una linea de UI.
public enum GameAction
{
    Up,
    Down,
    Left,
    Right,
    Confirm,   // X: aceptar / entrar / interactuar
    Cancel,    // Z: volver / cancelar
    Menu,      // Z (y Escape): abre el menu de pausa desde el mundo
    Run,       // Shift: correr en el mundo
    PageLeft,  // pestania anterior (Summary, bolsillos de la mochila)
    PageRight  // pestania siguiente
}
