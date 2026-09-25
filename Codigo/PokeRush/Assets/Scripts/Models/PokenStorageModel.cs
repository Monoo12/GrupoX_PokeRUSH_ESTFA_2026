using System.Collections.Generic;

// Adonde fue a parar un Pokemon que se consiguio (premio, liberado, apuesta).
public enum DestinoDePokemon
{
    Equipo,
    Pc,
    // Equipo y PC llenos: quien lo entrega decide que hacer en su lugar (la
    // ruleta paga plata).
    SinLugar
}

public struct PokenRecibido
{
    public DestinoDePokemon Destino;
    // Caja del PC donde quedo (0 = la primera). -1 si no fue al PC.
    public int Caja;

    public PokenRecibido(DestinoDePokemon destino, int caja)
    {
        Destino = destino;
        Caja = caja;
    }

    public static PokenRecibido SinLugar => new PokenRecibido(DestinoDePokemon.SinLugar, -1);
}

// El PC de almacenamiento: cajas de 6x5 donde quedan los Pokemon que no entran
// en el equipo.
//
// Es un modelo puro, sin nada de Unity, igual que el resto de los modelos: asi
// las reglas se prueban desde un test sin abrir escena. Que se puede mover y que
// no (por ejemplo, no dejar el equipo sin nadie que pelee) vive en PcReglas.
//
// Vive dentro de PlayerModel, y SaveService lo guarda en la misma tabla que el
// equipo (player_poken), con la caja en BoxIndex.
public class PokenStorageModel
{
    public const int Columnas = 6;
    public const int Filas = 5;
    public const int LugaresPorCaja = Columnas * Filas;
    public const int CajasPorDefecto = 8;

    private readonly PokenModel[][] _cajas;

    public int Cajas => _cajas.Length;

    public PokenStorageModel(int cajas = CajasPorDefecto)
    {
        if (cajas < 1) cajas = 1;
        _cajas = new PokenModel[cajas][];
        for (int i = 0; i < cajas; i++) _cajas[i] = new PokenModel[LugaresPorCaja];
    }

    public bool EsLugarValido(int caja, int lugar)
        => caja >= 0 && caja < Cajas && lugar >= 0 && lugar < LugaresPorCaja;

    public PokenModel Get(int caja, int lugar)
        => EsLugarValido(caja, lugar) ? _cajas[caja][lugar] : null;

    // Pone un Pokemon en un lugar (null lo vacia). Pisa lo que hubiera: quien
    // llama se encarga de no perder a nadie. Devuelve false si el lugar no existe.
    public bool Set(int caja, int lugar, PokenModel poken)
    {
        if (!EsLugarValido(caja, lugar)) return false;
        _cajas[caja][lugar] = poken;
        return true;
    }

    public PokenModel Sacar(int caja, int lugar)
    {
        PokenModel poken = Get(caja, lugar);
        if (poken != null) _cajas[caja][lugar] = null;
        return poken;
    }

    // Primer lugar libre, recorriendo las cajas en orden.
    public bool TryAgregar(PokenModel poken, out int caja, out int lugar)
    {
        caja = -1;
        lugar = -1;
        if (poken == null) return false;

        for (int c = 0; c < Cajas; c++)
            if (TryAgregarEnCaja(c, poken, out lugar)) { caja = c; return true; }

        return false;
    }

    public bool TryAgregarEnCaja(int caja, PokenModel poken, out int lugar)
    {
        lugar = -1;
        if (poken == null || caja < 0 || caja >= Cajas) return false;

        for (int l = 0; l < LugaresPorCaja; l++)
        {
            if (_cajas[caja][l] != null) continue;
            _cajas[caja][l] = poken;
            lugar = l;
            return true;
        }
        return false;
    }

    public int CountEnCaja(int caja)
    {
        if (caja < 0 || caja >= Cajas) return 0;
        int n = 0;
        foreach (PokenModel p in _cajas[caja]) if (p != null) n++;
        return n;
    }

    public int Count
    {
        get
        {
            int n = 0;
            for (int c = 0; c < Cajas; c++) n += CountEnCaja(c);
            return n;
        }
    }

    public bool EstaLleno => Count >= Cajas * LugaresPorCaja;

    public IEnumerable<(int caja, int lugar, PokenModel poken)> Todos()
    {
        for (int c = 0; c < Cajas; c++)
            for (int l = 0; l < LugaresPorCaja; l++)
                if (_cajas[c][l] != null) yield return (c, l, _cajas[c][l]);
    }

    public bool Contiene(System.Predicate<PokenModel> que)
    {
        if (que == null) return false;
        foreach (var entrada in Todos())
            if (que(entrada.poken)) return true;
        return false;
    }

    public static string NombreDeCaja(int caja) => $"Caja {caja + 1}";
}
