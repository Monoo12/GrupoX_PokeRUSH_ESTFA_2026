// Resultado de un giro de ruleta.
public class RouletteSpinResult
{
    public int PremioIndex = -1;
    public RoulettePrize Premio;
}

// Motor de la ruleta: elige la casilla. Nada mas.
//
// C# puro y sin UI, igual que el de la tragaperras, y con el mismo IBattleRng
// del combate para que los tests den siempre lo mismo. Quien aplica el premio es
// RouletteRewardService; quien lo anima es la pantalla.
public class RouletteEngine
{
    private readonly RouletteTableDefinition _def;

    public RouletteEngine(RouletteTableDefinition definicion)
    {
        _def = definicion;
    }

    public RouletteTableDefinition Definicion => _def;

    public RouletteSpinResult Girar(IBattleRng rng)
    {
        var resultado = new RouletteSpinResult();
        if (_def == null || rng == null) return resultado;

        int total = _def.PesoTotal;
        if (total <= 0) return resultado;

        int tirada = rng.Range(0, total);
        for (int i = 0; i < _def.premios.Length; i++)
        {
            RoulettePrize p = _def.premios[i];
            if (p == null || p.peso <= 0) continue;

            tirada -= p.peso;
            if (tirada >= 0) continue;

            resultado.PremioIndex = i;
            resultado.Premio = p;
            return resultado;
        }

        // Solo por redondeo raro: la ultima con peso.
        for (int i = _def.premios.Length - 1; i >= 0; i--)
        {
            if (_def.premios[i] == null || _def.premios[i].peso <= 0) continue;
            resultado.PremioIndex = i;
            resultado.Premio = _def.premios[i];
            return resultado;
        }

        return resultado;
    }

    // Probabilidad de una casilla, para balancear desde los tests.
    public float Probabilidad(int indice)
    {
        int total = _def != null ? _def.PesoTotal : 0;
        if (total <= 0 || _def.premios == null) return 0f;
        if (indice < 0 || indice >= _def.premios.Length) return 0f;

        RoulettePrize p = _def.premios[indice];
        return p == null ? 0f : p.peso / (float)total;
    }
}
