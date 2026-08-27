using UnityEngine;

// Un simbolo de la maquina de slots.
//
// El icono es OPCIONAL a proposito: mientras el equipo de arte no tenga los
// sprites del casino, la pantalla dibuja el color y la letra y la maquina se
// puede jugar igual. Cuando aparezcan los sprites se sueltan aca y no hay que
// tocar ni una linea de codigo.
[System.Serializable]
public class SlotSymbol
{
    public string nombre = "Simbolo";

    [Tooltip("Sprite del simbolo. Vacio = se dibuja el color y la letra de abajo.")]
    public Sprite icono;
    [Tooltip("Color de la casilla cuando no hay icono.")]
    public Color color = new Color(0.30f, 0.34f, 0.44f, 1f);
    [Tooltip("Letra que se muestra cuando no hay icono.")]
    public string letra = "?";

    [Tooltip("Cuanto sale este simbolo comparado con los demas. 0 = nunca sale.")]
    [Min(0)] public int peso = 10;

    [Tooltip("Comodin: cuenta como cualquier simbolo para armar una linea.")]
    public bool comodin;

    [Tooltip("Multiplicador de la APUESTA por cantidad de iguales seguidos, " +
             "empezando en 2. Ej: [0, 5, 20] = 2 iguales no paga, 3 pagan 5x, 4 pagan 20x.")]
    public int[] pagos = new int[] { 0, 5 };

    // Cuanto paga esta cantidad de simbolos seguidos, en multiplos de la apuesta.
    public int PagoPorCantidad(int cantidad)
    {
        if (pagos == null) return 0;
        int i = cantidad - 2;
        if (i < 0 || i >= pagos.Length) return 0;
        return Mathf.Max(0, pagos[i]);
    }
}

// Una linea de pago: que fila mira en cada rodillo.
//
// Con 3 rodillos y 3 filas, { 1, 1, 1 } es la linea del medio y { 0, 1, 2 } es
// una diagonal. Se configuran desde el Inspector para poder agregar o sacar
// lineas sin tocar el motor.
[System.Serializable]
public class SlotPayline
{
    public string nombre = "Linea";
    [Tooltip("Fila que mira la linea en cada rodillo (0 = la de arriba). " +
             "Tiene que tener un valor por rodillo.")]
    public int[] filas = new int[] { 1, 1, 1 };
}

// Configuracion completa de una maquina tragamonedas.
//
// Todo lo que define como se juega y cuanto paga vive aca, en un asset: dos
// maquinas del piso 1 pueden tener pagos distintos sin duplicar codigo, y
// balancear el casino es tocar numeros en el Inspector, no scripts.
[CreateAssetMenu(fileName = "NuevaMaquinaDeSlots", menuName = "PokeRush/Casino/Maquina de Slots")]
public class SlotMachineDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string maquinaNombre = "TRAGAMONEDAS";
    [TextArea(2, 3)] public string descripcion;

    [Header("Grilla")]
    [Tooltip("Columnas que giran.")]
    [Range(3, 5)] public int rodillos = 3;
    [Tooltip("Simbolos visibles por rodillo.")]
    [Range(1, 5)] public int filas = 3;

    [Header("Apuesta")]
    [Min(1)] public int apuestaMinima = 10;
    [Min(1)] public int apuestaMaxima = 100;
    [Tooltip("De a cuanto sube y baja la apuesta.")]
    [Min(1)] public int pasoDeApuesta = 10;

    [Header("Simbolos y lineas")]
    public SlotSymbol[] simbolos;
    public SlotPayline[] lineas;

    [Header("Presentacion")]
    [Tooltip("Cuanto gira el primer rodillo antes de frenar.")]
    [Min(0.1f)] public float duracionDelGiro = 1f;
    [Tooltip("Cuanto tarda cada rodillo siguiente en frenar despues del anterior.")]
    [Min(0f)] public float retrasoEntreRodillos = 0.35f;

    public int PesoTotal
    {
        get
        {
            if (simbolos == null) return 0;
            int total = 0;
            foreach (SlotSymbol s in simbolos)
                if (s != null && s.peso > 0) total += s.peso;
            return total;
        }
    }

    // Deja la apuesta dentro de los limites y pegada al paso configurado.
    public int AjustarApuesta(int apuesta)
    {
        int min = Mathf.Max(1, apuestaMinima);
        int max = Mathf.Max(min, apuestaMaxima);
        return Mathf.Clamp(apuesta, min, max);
    }

    // Chequeo de configuracion. Lo usan el smoke test y la maquina del mundo
    // para avisar en el momento en vez de tirar una tirada sin sentido.
    public bool Validar(out string error)
    {
        if (simbolos == null || simbolos.Length == 0)
        { error = "no tiene simbolos cargados"; return false; }

        if (PesoTotal <= 0)
        { error = "todos los simbolos tienen peso 0: nunca saldria ninguno"; return false; }

        if (lineas == null || lineas.Length == 0)
        { error = "no tiene lineas de pago"; return false; }

        for (int i = 0; i < lineas.Length; i++)
        {
            SlotPayline linea = lineas[i];
            if (linea == null || linea.filas == null || linea.filas.Length != rodillos)
            { error = $"la linea {i} no tiene una fila por rodillo ({rodillos})"; return false; }

            foreach (int fila in linea.filas)
                if (fila < 0 || fila >= filas)
                { error = $"la linea {i} apunta a la fila {fila}, fuera de rango (0..{filas - 1})"; return false; }
        }

        if (apuestaMinima > apuestaMaxima)
        { error = "la apuesta minima es mayor que la maxima"; return false; }

        error = null;
        return true;
    }
}
