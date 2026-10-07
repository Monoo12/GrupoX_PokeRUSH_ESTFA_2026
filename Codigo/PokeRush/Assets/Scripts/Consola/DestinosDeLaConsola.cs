using System.Collections.Generic;

// Los puntos de la historia a los que salta la consola de administrador
// ("ir casino2"), en orden. Cada uno dice que escena carga y que flags tienen
// que estar prendidos para que el juego crea que el jugador llego hasta ahi:
// los del recorrido (RecorridoDeLaHistoria), los pases de los ascensores y lo
// que cuenta Tixi al volver de cada nivel.
//
// Los flags se acumulan: llegar a "casino3" prende los de todos los de antes.
// Saltar para atras APAGA los de los puntos posteriores, asi "ir datacenter"
// desde el final vuelve a encontrar a Nestor sin liberar. Lo que no es un hito
// (un NPC vencido, un dialogo ya leido) queda como estaba.
//
// Clase pura: la usa la consola y la prueban los smoke tests sin abrir escenas.
public static class DestinosDeLaConsola
{
    public class Destino
    {
        // Lo que se escribe: "ir casino2".
        public string Clave;
        // El texto del boton.
        public string Nombre;
        public string Escena;
        // Los que se suman en ESTE punto (los de antes se prenden igual).
        public StoryFlag[] Flags;
        // Los objetos clave que el jugador ya tendria (por itemName). No traban
        // nada (los pases son flags), pero la mochila queda como en una partida.
        public string[] Objetos = new string[0];
    }

    public static readonly Destino[] Todos =
    {
        new Destino
        {
            Clave = "habitacion", Nombre = "Habitación", Escena = RecorridoDeLaHistoria.EscenaDelTutorial,
            Flags = new[] { StoryFlag.GameStarted, StoryFlag.ReceivedStarterPoken }
        },
        new Destino
        {
            Clave = "cueva", Nombre = "Cueva (inicio)", Escena = RecorridoDeLaHistoria.EscenaDelLobby,
            Flags = new[]
            {
                StoryFlag.Tutorial_Despertar, StoryFlag.Tutorial_Completado,
                StoryFlag.Historia_CaminoContado, StoryFlag.Lobby_TixiPresentado
            }
        },
        new Destino
        {
            Clave = "datacenter", Nombre = "Data Center", Escena = "NestorGOD2",
            Flags = new StoryFlag[0]
        },
        new Destino
        {
            Clave = "cueva2", Nombre = "Cueva (tras el DC)", Escena = RecorridoDeLaHistoria.EscenaDelLobby,
            Flags = new[]
            {
                StoryFlag.Historia_NestorPresentado, StoryFlag.Historia_Servidor01Desconectado,
                StoryFlag.Historia_NestorDesperto, StoryFlag.Torre1_NestorLiberado
            }
        },
        new Destino
        {
            Clave = "casino1", Nombre = "Casino, piso 1", Escena = "Casino Nivel 1",
            Flags = new[] { StoryFlag.Historia_TixiVueltaDelDataCenter }
        },
        new Destino
        {
            Clave = "casino2", Nombre = "Casino, piso 2", Escena = "Casino Nivel 2",
            Flags = new[] { StoryFlag.Casino_Piso1_PaseAscensor, StoryFlag.Casino_Piso1_Superado }
        },
        new Destino
        {
            Clave = "casino3", Nombre = "Casino, piso 3", Escena = "Casino nivel 3",
            Flags = new[] { StoryFlag.Casino_Piso2_PaseAscensor, StoryFlag.Casino_Piso2_Superado },
            Objetos = new[] { "Tarjeta VIP" }
        },
        new Destino
        {
            Clave = "casinofinal", Nombre = "Casino, Reymar", Escena = "Casino piso FINAL",
            Flags = new[] { StoryFlag.Casino_TacconiVencido, StoryFlag.Casino_Piso3_Superado },
            Objetos = new[] { "Tarjeta de acceso P4" }
        },
        new Destino
        {
            Clave = "cueva3", Nombre = "Cueva (tras el casino)", Escena = RecorridoDeLaHistoria.EscenaDelLobby,
            Flags = new[]
            {
                StoryFlag.Historia_ReymarPresentado, StoryFlag.Casino_JefeFinalVencido,
                StoryFlag.Historia_ReymarServidor, StoryFlag.Historia_Servidor02Desconectado,
                StoryFlag.Casino_Completado
            }
        },
        new Destino
        {
            Clave = "julieta", Nombre = "Julieta", Escena = "BossFinal",
            Flags = new[] { StoryFlag.Historia_TixiVueltaDelCasino, StoryFlag.Historia_TixiTransmisor },
            Objetos = new[] { "Transmisor de Tixi" }
        }
    };

    // Por clave o por un pedazo que no sea ambiguo ("cas" no, "casinof" si).
    public static Destino Buscar(string texto)
    {
        string buscado = ComandosDeLaConsola.Normalizar(texto);
        if (buscado.Length == 0) return null;

        Destino unico = null;
        int cuantos = 0;
        foreach (Destino d in Todos)
        {
            if (d.Clave == buscado) return d;
            if (d.Clave.StartsWith(buscado)) { unico = d; cuantos++; }
        }
        return cuantos == 1 ? unico : null;
    }

    // Deja la historia exactamente en ese punto: prende los flags hasta el
    // destino y apaga los de los destinos que vienen despues.
    public static void MarcarHistoria(StoryModel historia, Destino destino)
    {
        if (historia == null || destino == null) return;

        bool despues = false;
        foreach (Destino d in Todos)
        {
            foreach (StoryFlag f in d.Flags)
            {
                if (despues) historia.ActiveFlags.Remove(f);
                else historia.ActivateFlag(f);
            }
            if (d == destino) despues = true;
        }
    }

    // Los objetos clave de todos los puntos hasta el destino, inclusive. Para
    // atras no se sacan: la mochila no tiene como quitar un objeto clave.
    public static IEnumerable<string> ObjetosHasta(Destino destino)
    {
        foreach (Destino d in Todos)
        {
            foreach (string o in d.Objetos) yield return o;
            if (d == destino) yield break;
        }
    }

    public static IEnumerable<string> Claves()
    {
        foreach (Destino d in Todos) yield return d.Clave;
    }
}
