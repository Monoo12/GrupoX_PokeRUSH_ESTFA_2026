using UnityEngine;

// Las reglas de la tienda de Tixi, sin pantalla: cuanto sale, cuanto alcanza y
// la compra en si. Viven aparte para que la pantalla y los smoke tests no
// tengan dos ideas distintas de que es "comprar".
//
// El precio es el 'buyPrice' del objeto: el mismo numero que ya tiene cada
// HealItemDefinition, asi un cambio de precio se hace en un solo lugar.
public static class ReglasDeTienda
{
    // Tope por compra, como el contador de dos cifras de los juegos.
    public const int CantidadMaxima = 99;

    public static int Precio(HealItemDefinition objeto)
        => objeto != null ? Mathf.Max(0, objeto.buyPrice) : 0;

    public static int Total(HealItemDefinition objeto, int cantidad)
        => Precio(objeto) * Mathf.Max(0, cantidad);

    // Cuantos se pueden comprar con esa plata (0 = ni uno).
    public static int MaximoQueAlcanza(int plata, HealItemDefinition objeto)
    {
        int precio = Precio(objeto);
        if (objeto == null) return 0;
        if (precio <= 0) return CantidadMaxima;
        return Mathf.Clamp(plata / precio, 0, CantidadMaxima);
    }

    // Cobra y mete los objetos en la mochila. Devuelve false sin tocar nada si
    // no alcanza la plata o el pedido no tiene sentido.
    public static bool Comprar(PlayerModel jugador, HealItemDefinition objeto, int cantidad)
    {
        if (jugador == null || objeto == null || cantidad <= 0 || cantidad > CantidadMaxima) return false;

        int total = Total(objeto, cantidad);
        if (jugador.Money < total) return false;

        jugador.SpendMoney(total);
        jugador.Inventory.AddHealItem(objeto, cantidad);
        return true;
    }
}
