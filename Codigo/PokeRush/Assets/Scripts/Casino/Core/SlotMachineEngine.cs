using System.Collections.Generic;

// Una linea que pago en una tirada.
public class SlotLineHit
{
    public int LineaIndex;
    public int SimboloIndex;
    // Cuantos iguales seguidos desde el primer rodillo.
    public int Cantidad;
    // Lo que pago esta linea, ya multiplicado por la apuesta.
    public int Pago;
    public string SimboloNombre;
}

// Resultado completo de una tirada.
public class SlotSpinResult
{
    // [rodillo, fila] -> indice dentro de SlotMachineDefinition.simbolos.
    public int[,] Grilla;
    public List<SlotLineHit> Lineas = new List<SlotLineHit>();
    public int Apuesta;
    // Bruto que devuelve la maquina (0 si no pago nada).
    public int Pago;
    // Lo que efectivamente cambia el bolsillo del jugador.
    public int Neto => Pago - Apuesta;
    public bool Gano => Pago > 0;
}

// Motor de la maquina tragamonedas. C# puro, sin MonoBehaviour y sin UI: la
// pantalla le pide una tirada y despues la anima como quiera.
//
// El azar entra por IBattleRng, la misma interfaz que ya usa el combate. Con
// SeededRng los tests dan siempre el mismo resultado; el juego usa UnityRng.
//
// De 'highnet/Lucky-Slots-v1' se toma la idea de evaluar la grilla por lineas
// de pago con conteo de iguales desde el primer rodillo. Lo que NO se trae es
// su implementacion: alla la logica vive repartida en StateMachineBehaviours de
// un Animator y se busca todo con FindGameObjectWithTag, ademas de sortear los
// simbolos con probabilidad uniforme. Aca hace falta poder balancear los pesos
// desde el Inspector y correr el motor en un test sin escena.
public class SlotMachineEngine
{
    private readonly SlotMachineDefinition _def;

    public SlotMachineEngine(SlotMachineDefinition definicion)
    {
        _def = definicion;
    }

    public SlotMachineDefinition Definicion => _def;

    // Una tirada completa: sortea la grilla y calcula lo que paga.
    // No toca el dinero del jugador: de eso se ocupa quien la llama.
    public SlotSpinResult Girar(IBattleRng rng, int apuesta)
    {
        if (_def == null || rng == null) return new SlotSpinResult { Apuesta = apuesta };
        return EvaluarGrilla(SortearGrilla(rng), apuesta);
    }

    // Evalua una grilla YA sorteada. Separado de Girar para poder probar los
    // pagos con grillas armadas a mano, sin depender del azar.
    public SlotSpinResult EvaluarGrilla(int[,] grilla, int apuesta)
    {
        var resultado = new SlotSpinResult { Apuesta = apuesta, Grilla = grilla };
        if (_def == null || grilla == null) return resultado;

        Evaluar(resultado, apuesta);
        return resultado;
    }

    // Sortea cada casilla por separado con los pesos configurados.
    public int[,] SortearGrilla(IBattleRng rng)
    {
        var grilla = new int[_def.rodillos, _def.filas];
        for (int r = 0; r < _def.rodillos; r++)
            for (int f = 0; f < _def.filas; f++)
                grilla[r, f] = SortearSimbolo(rng);
        return grilla;
    }

    // Indice de simbolo al azar, respetando los pesos.
    public int SortearSimbolo(IBattleRng rng)
    {
        int total = _def.PesoTotal;
        if (total <= 0) return 0;

        int tirada = rng.Range(0, total);
        for (int i = 0; i < _def.simbolos.Length; i++)
        {
            SlotSymbol s = _def.simbolos[i];
            if (s == null || s.peso <= 0) continue;

            tirada -= s.peso;
            if (tirada < 0) return i;
        }
        // Solo se llega aca por redondeo raro: devolvemos el ultimo con peso.
        for (int i = _def.simbolos.Length - 1; i >= 0; i--)
            if (_def.simbolos[i] != null && _def.simbolos[i].peso > 0) return i;
        return 0;
    }

    private void Evaluar(SlotSpinResult resultado, int apuesta)
    {
        if (_def.lineas == null) return;

        for (int i = 0; i < _def.lineas.Length; i++)
        {
            SlotLineHit hit = EvaluarLinea(resultado.Grilla, _def.lineas[i], i, apuesta);
            if (hit == null) continue;

            resultado.Lineas.Add(hit);
            resultado.Pago += hit.Pago;
        }
    }

    // Cuenta iguales seguidos desde el rodillo 0. Los comodines valen por
    // cualquier simbolo, y si la linea arranca con comodines el simbolo que
    // cuenta es el primero que no lo sea (una linea toda de comodines paga como
    // el comodin).
    private SlotLineHit EvaluarLinea(int[,] grilla, SlotPayline linea, int indice, int apuesta)
    {
        if (linea == null || linea.filas == null || linea.filas.Length < _def.rodillos) return null;

        int simboloBase = -1;
        int cantidad = 0;

        for (int r = 0; r < _def.rodillos; r++)
        {
            int fila = linea.filas[r];
            if (fila < 0 || fila >= _def.filas) return null;

            int indiceSimbolo = grilla[r, fila];
            SlotSymbol simbolo = SimboloEn(indiceSimbolo);
            if (simbolo == null) break;

            if (simboloBase < 0)
            {
                // Todavia no sabemos de que es la linea.
                if (!simbolo.comodin) simboloBase = indiceSimbolo;
                cantidad++;
                continue;
            }

            if (indiceSimbolo == simboloBase || simbolo.comodin) cantidad++;
            else break;
        }

        // Linea entera de comodines: cuenta como el comodin.
        if (simboloBase < 0 && cantidad > 0) simboloBase = grilla[0, linea.filas[0]];
        if (simboloBase < 0) return null;

        SlotSymbol ganador = SimboloEn(simboloBase);
        if (ganador == null) return null;

        int multiplicador = ganador.PagoPorCantidad(cantidad);
        if (multiplicador <= 0) return null;

        return new SlotLineHit
        {
            LineaIndex = indice,
            SimboloIndex = simboloBase,
            SimboloNombre = ganador.nombre,
            Cantidad = cantidad,
            Pago = multiplicador * apuesta
        };
    }

    public SlotSymbol SimboloEn(int indice)
    {
        if (_def == null || _def.simbolos == null) return null;
        if (indice < 0 || indice >= _def.simbolos.Length) return null;
        return _def.simbolos[indice];
    }

    // Devolucion teorica de la maquina (RTP): cuanto devuelve por cada moneda
    // apostada, en promedio, calculado a fuerza bruta sobre muchas tiradas.
    // Es una herramienta de balance para el equipo, no se usa en el juego.
    public float DevolucionEstimada(IBattleRng rng, int tiradas = 20000, int apuesta = 10)
    {
        if (tiradas <= 0 || apuesta <= 0) return 0f;

        long pagado = 0;
        for (int i = 0; i < tiradas; i++)
            pagado += Girar(rng, apuesta).Pago;

        return pagado / (float)(tiradas * (long)apuesta);
    }
}
