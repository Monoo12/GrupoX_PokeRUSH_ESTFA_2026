using System.Collections;
using System.Collections.Generic;

// Un DIRECTOR le pone guion a una pelea: dialogos en el medio del combate,
// reglas que cambian a mitad, un Pokemon del jugador que deja de obedecer.
//
// BattleController no sabe nada de ninguna pelea en particular: solo llama a
// estos ganchos en momentos fijos del flujo, y cada director decide que hace.
// Asi la pelea de Julieta vive entera en su carpeta (Scripts/Julieta) y el
// combate de siempre no cambia: sin director (lo normal) ningun gancho corre.
//
// Los ganchos que devuelven IEnumerator corren DENTRO de las corrutinas del
// combate (con yield return), asi que el combate espera a que terminen antes de
// seguir. Eso importa: el director puede vivir en un objeto del mapa, que
// MapBattleBridge apaga durante la pelea, y aun asi sus corrutinas corren,
// porque las ejecuta el BattleController.
//
// Mientras corre un gancho la fase del combate es CalculatingResult: los
// atajos de teclado del combate no hacen nada y el HUD no tiene menus abiertos,
// asi que una caja de dialogo puede quedarse con el input.
public interface IDirectorDeCombate
{
    // Terminada la intro (los dos Pokemon en el campo), antes del primer turno.
    IEnumerator AlEmpezar(BattleController combate);

    // Al empezar cada turno del jugador, antes de la Prediccion, la ruleta y el
    // menu de comandos. Tambien despues de elegir un reemplazo obligado.
    IEnumerator AntesDeElegir(BattleController combate);

    // El jugador ya eligio y el turno esta por resolverse. 'reemplazar' cambia
    // la accion del jugador (un Pokemon que no obedece hace otra cosa).
    IEnumerator AntesDeResolver(BattleController combate, BattleAction accionDelJugador,
                                System.Action<BattleAction> reemplazar);

    // El turno ya se reprodujo entero. Si el director termina la pelea aca
    // (BattleModel.Finish), el combate lo cierra a continuacion.
    IEnumerator DespuesDelTurno(BattleController combate, List<BattleEvent> eventos);

    // El motor esta por debilitar al Poken activo del rival: true lo deja en 1
    // PS. Se puede llamar varias veces por turno: tiene que ser idempotente.
    bool ImpedirQueCaiga(BattleModel modelo);

    // Avisos sueltos, para los disparadores del guion.
    void NestorIntervino();
    void RuletaCobro(EfectoDeRuleta efecto, bool alJugador);

    // El combate termino (antes del cartel de victoria o derrota).
    void AlTerminar(bool victoriaDelJugador);
}
