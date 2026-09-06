using UnityEngine;
using System.Collections.Generic;

// La mesa de la ruleta, sin nada de Unity adentro.
//
// Vive aparte de la pantalla a proposito: las reglas (que casillero es rojo,
// cuanto paga cada apuesta, donde cae la bola) se pueden probar desde un test
// sin abrir una escena, y la UI se queda solo con dibujar. Es la misma division
// que ya usa el casino entre SlotMachineEngine y SlotsScreen.
public static class RouletteBoard
{
    // Ruleta europea: 37 casilleros, un solo cero.
    public const int Casilleros = 37;

    // Orden REAL de la rueda europea, en sentido horario arrancando del cero.
    // No es 0,1,2,...: los numeros estan salteados a proposito para que los
    // pares y los rojos queden repartidos. El dibujo de roulette_wheel.png
    // tiene el cero verde arriba, asi que el indice 0 de esta tabla es el que
    // queda a las 12 en punto cuando la rueda esta sin rotar.
    public static readonly int[] Orden =
    {
        0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23,
        10, 5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26
    };

    private static readonly HashSet<int> Rojos = new HashSet<int>
    {
        1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36
    };

    // Grados que ocupa cada casillero.
    //
    // El dibujo de la rueda respeta esta misma tabla: roulette_wheel.png sale
    // de ruleta_base_2.png con el anillo REPINTADO, porque el original era una
    // ilustracion con 36 casilleros y una secuencia que se salteaba el 23, el
    // 10 y el 24. Se repinto con estos 37 en sentido horario, asi que la bola
    // frena sobre el numero que se canta. Ver repintar_anillo() en
    // scripts/limpiar_ruleta.py; si alguien cambia Orden hay que volver a
    // correr ese script o la rueda deja de coincidir.
    public const float PasoDeCasillero = 360f / Casilleros;

    public static bool EsRojo(int numero) => Rojos.Contains(numero);

    public static bool EsNegro(int numero) => numero != 0 && !Rojos.Contains(numero);

    public static RouletteColor ColorDe(int numero)
    {
        if (numero == 0) return RouletteColor.Verde;
        return EsRojo(numero) ? RouletteColor.Rojo : RouletteColor.Negro;
    }

    // Donde esta dibujado un numero dentro de la rueda, en grados horarios
    // desde arriba. Lo usa la pantalla para frenar la bola en el lugar exacto.
    public static float AnguloDe(int numero)
    {
        int i = IndiceEnLaRueda(numero);
        return i < 0 ? 0f : i * PasoDeCasillero;
    }

    public static int IndiceEnLaRueda(int numero)
    {
        for (int i = 0; i < Orden.Length; i++)
            if (Orden[i] == numero) return i;
        return -1;
    }

    public static float NormalizarAngulo(float grados)
    {
        grados %= 360f;
        return grados < 0f ? grados + 360f : grados;
    }

    // Donde va a quedar dibujado el casillero ganador una vez que la rueda
    // termino de girar.
    public static float AnguloFinalDelCasillero(int ganador, float anguloRuedaFinal)
        => NormalizarAngulo(AnguloDe(ganador) + anguloRuedaFinal);

    // Cuanto tiene que recorrer la bola para caer CLAVADA en el casillero
    // ganador, dando 'vueltas' giros enteros al reves que la rueda.
    //
    // Esta es la cuenta que hace que el numero cantado coincida con lo que se
    // ve: sin ella la bola frena en cualquier lado. Vive aca y no en la
    // pantalla para poder probarla sin abrir una escena (ver
    // RouletteSmokeTests.TestLaBolaCaeEnElCasilleroSorteado).
    //
    // Devuelve un valor NEGATIVO: la bola gira al reves que la rueda.
    public static float GiroDeLaBola(int ganador, float anguloRuedaFinal,
                                     float anguloBolaActual, int vueltas)
    {
        float destino = AnguloFinalDelCasillero(ganador, anguloRuedaFinal);
        float falta = NormalizarAngulo(anguloBolaActual - destino);
        return -(Mathf.Max(0, vueltas) * 360f + falta);
    }

    // Cuanto paga una apuesta ganada, SIN contar la ficha apostada.
    //
    // Son los pagos de la ruleta de verdad: al pleno 35 a 1 y a color 1 a 1.
    // El jugador se lleva la apuesta mas esto.
    public static int PagoDe(RouletteBetKind tipo)
    {
        switch (tipo)
        {
            case RouletteBetKind.Numero: return 35;
            case RouletteBetKind.Rojo:
            case RouletteBetKind.Negro: return 1;
            default: return 0;
        }
    }

    // Si una apuesta cobra con el numero que salio.
    //
    // Ojo con el cero: no es ni rojo ni negro, asi que se lleva puestas las dos
    // apuestas de color. Es la regla de la ruleta y es lo que le da la ventaja
    // a la casa; sin eso, apostar a color seria plata gratis.
    public static bool Gana(RouletteBet apuesta, int numero)
    {
        switch (apuesta.Kind)
        {
            case RouletteBetKind.Numero: return apuesta.Numero == numero;
            case RouletteBetKind.Rojo: return EsRojo(numero);
            case RouletteBetKind.Negro: return EsNegro(numero);
            default: return false;
        }
    }
}
