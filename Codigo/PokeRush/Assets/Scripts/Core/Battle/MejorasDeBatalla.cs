using System.Collections.Generic;
using System.Text;

// Las mejoras que da la ruleta de Martin en la Quarcho Cueva: un stat de tu
// equipo arranca subido durante UNA batalla, la proxima que pelees.
//
// Los valores nuevos van SIEMPRE al final: los premios se guardan en assets
// (CasinoPrizeTable) y meter uno en el medio le cambia la mejora a todos los de
// abajo.
public enum MejoraDeBatalla
{
    Ninguna,
    Ataque,
    Defensa,
    AtaqueEspecial,
    DefensaEspecial,
    Velocidad,
    Precision,
    // Las cinco de pelea juntas (ataque, defensa, especiales y velocidad).
    Todas
}

// Una mejora ganada y todavia sin usar.
[System.Serializable]
public struct MejoraPendiente
{
    public MejoraDeBatalla Mejora;
    public int Etapas;

    public MejoraPendiente(MejoraDeBatalla mejora, int etapas)
    {
        Mejora = mejora;
        Etapas = etapas;
    }
}

// Las reglas, sin UI ni escena: las usan el PlayerModel (guardarlas), el
// BattleModel (aplicarlas) y los smoke tests.
//
// Por que "a cada Poken que entra" y no "al que arranca": los stages viven en
// BattlePokenState, que se recrea en cada cambio. Aplicadas solo al primero,
// la mejora se perderia al primer cambio y la ruleta valdria la mitad segun a
// quien mandes adelante. Asi la mejora es del EQUIPO durante toda la batalla.
public static class MejorasDeBatalla
{
    // Tope de stages de la saga.
    public const int EtapasMaximas = 6;

    private static readonly Stat[] LasCincoDePelea =
    {
        Stat.Attack, Stat.Defense, Stat.SpecialAttack, Stat.SpecialDefense, Stat.Speed
    };

    // Que stats toca cada mejora.
    public static Stat[] StatsDe(MejoraDeBatalla mejora)
    {
        switch (mejora)
        {
            case MejoraDeBatalla.Ataque: return new[] { Stat.Attack };
            case MejoraDeBatalla.Defensa: return new[] { Stat.Defense };
            case MejoraDeBatalla.AtaqueEspecial: return new[] { Stat.SpecialAttack };
            case MejoraDeBatalla.DefensaEspecial: return new[] { Stat.SpecialDefense };
            case MejoraDeBatalla.Velocidad: return new[] { Stat.Speed };
            case MejoraDeBatalla.Precision: return new[] { Stat.Accuracy };
            case MejoraDeBatalla.Todas: return (Stat[])LasCincoDePelea.Clone();
            default: return new Stat[0];
        }
    }

    public static string Nombre(MejoraDeBatalla mejora)
    {
        switch (mejora)
        {
            case MejoraDeBatalla.Ataque: return "Ataque";
            case MejoraDeBatalla.Defensa: return "Defensa";
            case MejoraDeBatalla.AtaqueEspecial: return "Ataque Especial";
            case MejoraDeBatalla.DefensaEspecial: return "Defensa Especial";
            case MejoraDeBatalla.Velocidad: return "Velocidad";
            case MejoraDeBatalla.Precision: return "Precisión";
            case MejoraDeBatalla.Todas: return "todas sus estadísticas";
            default: return "";
        }
    }

    // "+1 Ataque"
    public static string Describir(MejoraPendiente m)
        => m.Mejora == MejoraDeBatalla.Ninguna ? "" : $"+{m.Etapas} {Nombre(m.Mejora)}";

    // "+1 Ataque y +2 Velocidad"
    public static string Describir(IList<MejoraPendiente> mejoras)
    {
        if (mejoras == null || mejoras.Count == 0) return "";

        var partes = new List<string>();
        foreach (MejoraPendiente m in mejoras)
        {
            string d = Describir(m);
            if (!string.IsNullOrEmpty(d)) partes.Add(d);
        }

        if (partes.Count <= 1) return partes.Count == 1 ? partes[0] : "";
        return string.Join(", ", partes.GetRange(0, partes.Count - 1)) + " y " + partes[partes.Count - 1];
    }

    // Suma una mejora a la lista: si ya habia una del mismo tipo, se acumulan
    // las etapas (hasta el tope) en vez de sumar otra entrada.
    public static void Sumar(List<MejoraPendiente> lista, MejoraDeBatalla mejora, int etapas)
    {
        if (lista == null || mejora == MejoraDeBatalla.Ninguna || etapas <= 0) return;

        for (int i = 0; i < lista.Count; i++)
        {
            if (lista[i].Mejora != mejora) continue;
            int total = lista[i].Etapas + etapas;
            lista[i] = new MejoraPendiente(mejora, total > EtapasMaximas ? EtapasMaximas : total);
            return;
        }
        lista.Add(new MejoraPendiente(mejora, etapas > EtapasMaximas ? EtapasMaximas : etapas));
    }

    // Aplica las mejoras al Poken que acaba de entrar. Sin eventos ni
    // carteles: el combate anuncia las mejoras una sola vez, al arrancar.
    public static void Aplicar(BattlePokenState estado, IList<MejoraPendiente> mejoras)
    {
        if (estado == null || mejoras == null) return;
        foreach (MejoraPendiente m in mejoras)
            foreach (Stat stat in StatsDe(m.Mejora))
                estado.ModifyStage(stat, m.Etapas);
    }

    // ---------- guardado ----------
    // Texto y no numeros ("Ataque:1;Velocidad:2"), como los flags: agregar
    // mejoras al enum no rompe los guardados viejos.

    public static string Serializar(IList<MejoraPendiente> mejoras)
    {
        if (mejoras == null || mejoras.Count == 0) return "";

        var sb = new StringBuilder();
        foreach (MejoraPendiente m in mejoras)
        {
            if (m.Mejora == MejoraDeBatalla.Ninguna || m.Etapas <= 0) continue;
            if (sb.Length > 0) sb.Append(';');
            sb.Append(m.Mejora).Append(':').Append(m.Etapas);
        }
        return sb.ToString();
    }

    public static List<MejoraPendiente> Deserializar(string texto)
    {
        var lista = new List<MejoraPendiente>();
        if (string.IsNullOrEmpty(texto)) return lista;

        foreach (string parte in texto.Split(';'))
        {
            string[] kv = parte.Split(':');
            if (kv.Length != 2) continue;
            if (!System.Enum.TryParse(kv[0], out MejoraDeBatalla mejora)) continue;
            if (!int.TryParse(kv[1], out int etapas)) continue;
            Sumar(lista, mejora, etapas);
        }
        return lista;
    }
}
