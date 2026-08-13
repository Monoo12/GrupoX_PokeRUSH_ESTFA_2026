using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Secuencias visuales del combate: transicion de inicio, intro del entrenador,
// lanzamiento/apertura de Poke Ball, aparicion/retirada/debilitamiento de los
// Pokens y pulsos de color (stats/estados). Solo presentacion: lo llama
// BattleController desde sus corrutinas; no toca reglas ni modelos.
public class BattleFxView : MonoBehaviour
{
    [Header("Referencias de escena")]
    [SerializeField] private SpriteRenderer trainerRenderer;   // sprite del entrenador rival (oculto)
    [SerializeField] private SpriteRenderer ballRenderer;      // la ball volando (oculta)
    [SerializeField] private SpriteRenderer burstRenderer;     // destello al abrirse (oculto)
    [SerializeField] private Image fadeOverlay;                // Image fullscreen para fundidos

    [Header("Sprites de la Poke Ball")]
    [SerializeField] private Sprite[] ballSpinFrames;          // tira de rotacion
    [SerializeField] private Sprite ballOpenSprite;            // ball abierta

    [Header("Ataques")]
    [SerializeField] private SpriteRenderer projectileRenderer; // proyectil (oculto)
    [SerializeField] private SpriteRenderer impactRenderer;     // destello de impacto (oculto)

    [Header("Clima y terreno")]
    [SerializeField] private Image weatherOverlay;   // tinte persistente del clima
    [SerializeField] private Image terrainOverlay;   // tinte persistente del terreno

    [Tooltip("Camara del combate, para el temblor de pantalla. Sin esto se usa " +
             "Camera.main, que en un mapa es la camara de exploracion: el " +
             "temblor le movería la camara al mundo en vez de a la pelea.")]
    [SerializeField] private Camera battleCamera;

    // La del prefab si esta, si no la principal de la escena
    private Camera Cam => battleCamera != null ? battleCamera : Camera.main;

    [Header("Tiempos")]
    [SerializeField] private float throwDuration = 0.55f;
    [SerializeField] private float appearDuration = 0.30f;
    [SerializeField] private float recallDuration = 0.30f;
    [SerializeField] private float faintDuration = 0.55f;
    [SerializeField] private float trainerSlideDuration = 0.45f;

    // Colores de pulso por situacion (los usa BattleController)
    public static readonly Color PulseStatUp = new Color(1f, 0.85f, 0.4f);
    public static readonly Color PulseStatDown = new Color(0.45f, 0.6f, 1f);
    public static readonly Color PulseHeal = new Color(0.5f, 1f, 0.55f);

    public static Color PulseForStatus(StatusEffect status)
    {
        switch (status)
        {
            case StatusEffect.Burned: return new Color(1f, 0.45f, 0.25f);
            case StatusEffect.Poisoned: return new Color(0.75f, 0.4f, 0.9f);
            case StatusEffect.Paralyzed: return new Color(1f, 0.95f, 0.3f);
            case StatusEffect.Asleep: return new Color(0.6f, 0.6f, 0.65f);
            case StatusEffect.Frozen: return new Color(0.5f, 0.9f, 1f);
            default: return Color.white;
        }
    }

    // ---------- transicion de inicio ----------

    // Doble flash blanco + fundido a negro + apertura, como en la saga
    public IEnumerator ScreenTransition()
    {
        if (fadeOverlay == null) yield break;
        fadeOverlay.gameObject.SetActive(true);

        for (int i = 0; i < 2; i++)
        {
            yield return FadeOverlayTo(Color.white, 0.55f, 0.07f);
            yield return FadeOverlayTo(Color.white, 0f, 0.07f);
        }
        yield return FadeOverlayTo(Color.black, 1f, 0.18f);
        yield return new WaitForSeconds(0.15f);
        yield return FadeOverlayTo(Color.black, 0f, 0.25f);

        fadeOverlay.gameObject.SetActive(false);
    }

    private IEnumerator FadeOverlayTo(Color baseColor, float targetAlpha, float duration)
    {
        float startAlpha = fadeOverlay.color.a;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float alpha = Mathf.Lerp(startAlpha, targetAlpha, t / duration);
            fadeOverlay.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }
        fadeOverlay.color = new Color(baseColor.r, baseColor.g, baseColor.b, targetAlpha);
    }

    // ---------- intro del entrenador ----------

    // El entrenador entra deslizandose hasta la posicion del rival y queda visible.
    // (El mensaje "¡X te desafia!" lo muestra el controller mientras tanto.)
    public IEnumerator TrainerSlideIn(Sprite trainerSprite, Vector3 platformPosition)
    {
        if (trainerRenderer == null || trainerSprite == null) yield break;

        trainerRenderer.sprite = trainerSprite;
        Vector3 from = platformPosition + Vector3.right * 4f;
        trainerRenderer.transform.position = from;
        trainerRenderer.gameObject.SetActive(true);

        yield return MoveOver(trainerRenderer.transform, from, platformPosition, trainerSlideDuration);
    }

    // El entrenador se retira por la derecha (lanza a su Poken al salir)
    public IEnumerator TrainerSlideOut()
    {
        if (trainerRenderer == null || !trainerRenderer.gameObject.activeSelf) yield break;

        Vector3 from = trainerRenderer.transform.position;
        Vector3 to = from + Vector3.right * 4f;
        yield return MoveOver(trainerRenderer.transform, from, to, trainerSlideDuration);
        trainerRenderer.gameObject.SetActive(false);
    }

    // ---------- Poke Ball: lanzar, abrir y aparecer ----------

    // Ball en arco desde el lado del lanzador hasta la plataforma del Poken,
    // apertura con destello, y el Poken aparece creciendo desde el piso.
    public IEnumerator ThrowBallAndAppear(PokenView pokenView, bool fromPlayerSide)
    {
        Vector3 target = pokenView.transform.position;
        Vector3 from = fromPlayerSide
            ? target + new Vector3(-3.5f, 0.6f, 0f)
            : target + new Vector3(3.5f, 0.9f, 0f);

        // vuelo en parabola con la ball girando
        if (ballRenderer != null && ballSpinFrames != null && ballSpinFrames.Length > 0)
        {
            ballRenderer.gameObject.SetActive(true);
            float frameTime = 0f;
            int frame = 0;
            for (float t = 0f; t < throwDuration; t += Time.deltaTime)
            {
                float k = t / throwDuration;
                Vector3 pos = Vector3.Lerp(from, target, k);
                pos.y += Mathf.Sin(k * Mathf.PI) * 1.1f; // arco
                ballRenderer.transform.position = pos;

                frameTime += Time.deltaTime;
                if (frameTime > 0.05f)
                {
                    frameTime = 0f;
                    frame = (frame + 1) % ballSpinFrames.Length;
                    ballRenderer.sprite = ballSpinFrames[frame];
                }
                yield return null;
            }

            // apertura + destello
            if (ballOpenSprite != null) ballRenderer.sprite = ballOpenSprite;
            ballRenderer.transform.position = target;
            if (burstRenderer != null)
            {
                burstRenderer.transform.position = target + Vector3.up * 0.3f;
                burstRenderer.gameObject.SetActive(true);
            }
            yield return new WaitForSeconds(0.12f);
            if (burstRenderer != null) burstRenderer.gameObject.SetActive(false);
            ballRenderer.gameObject.SetActive(false);
        }

        // el Poken aparece creciendo desde el piso (pivot en los pies)
        yield return AppearGrow(pokenView);
    }

    // Aparicion de un salvaje: surge del entorno con un fade + saltito
    public IEnumerator WildAppear(PokenView pokenView)
    {
        SpriteRenderer renderer = pokenView.Renderer;
        pokenView.Show();
        if (renderer == null) yield break;

        Vector3 basePos = pokenView.transform.position;
        for (float t = 0f; t < appearDuration * 1.6f; t += Time.deltaTime)
        {
            float k = t / (appearDuration * 1.6f);
            renderer.color = new Color(1f, 1f, 1f, k);
            pokenView.transform.position = basePos + Vector3.up * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * 0.15f;
            yield return null;
        }
        renderer.color = Color.white;
        pokenView.transform.position = basePos;
    }

    private IEnumerator AppearGrow(PokenView pokenView)
    {
        Vector3 fullScale = pokenView.transform.localScale;
        pokenView.transform.localScale = Vector3.zero;
        pokenView.Show();
        ResetRendererColor(pokenView);

        for (float t = 0f; t < appearDuration; t += Time.deltaTime)
        {
            float k = t / appearDuration;
            // pequeño rebote al final (overshoot)
            float scale = k < 0.85f ? k / 0.85f * 1.08f : Mathf.Lerp(1.08f, 1f, (k - 0.85f) / 0.15f);
            pokenView.transform.localScale = fullScale * scale;
            yield return null;
        }
        pokenView.transform.localScale = fullScale;
    }

    // ---------- retirada y debilitamiento ----------

    // El Poken vuelve a su ball: se encoge hacia el piso y desaparece
    public IEnumerator Recall(PokenView pokenView)
    {
        Vector3 fullScale = pokenView.transform.localScale;
        for (float t = 0f; t < recallDuration; t += Time.deltaTime)
        {
            pokenView.transform.localScale = fullScale * (1f - t / recallDuration);
            yield return null;
        }
        pokenView.Hide();
        pokenView.transform.localScale = fullScale; // restaurar para el proximo
    }

    // Debilitado: se hunde mientras se desvanece
    public IEnumerator FaintAnim(PokenView pokenView)
    {
        SpriteRenderer renderer = pokenView.Renderer;
        Vector3 basePos = pokenView.transform.position;

        for (float t = 0f; t < faintDuration; t += Time.deltaTime)
        {
            float k = t / faintDuration;
            pokenView.transform.position = basePos + Vector3.down * k * 0.9f;
            if (renderer != null) renderer.color = new Color(1f, 1f, 1f, 1f - k);
            yield return null;
        }

        pokenView.Hide();
        pokenView.transform.position = basePos;
        if (renderer != null) renderer.color = Color.white;
    }

    // ---------- pulsos de color (stats, estados, curacion) ----------

    public IEnumerator PulseTint(PokenView pokenView, Color color, int pulses = 2)
    {
        SpriteRenderer renderer = pokenView.Renderer;
        if (renderer == null) yield break;

        const float pulseTime = 0.16f;
        for (int i = 0; i < pulses; i++)
        {
            for (float t = 0f; t < pulseTime; t += Time.deltaTime)
            {
                renderer.color = Color.Lerp(Color.white, color, t / pulseTime);
                yield return null;
            }
            for (float t = 0f; t < pulseTime; t += Time.deltaTime)
            {
                renderer.color = Color.Lerp(color, Color.white, t / pulseTime);
                yield return null;
            }
        }
        renderer.color = Color.white;
    }

    // ---------- clima y terreno (overlays persistentes) ----------

    // Tinte de pantalla mientras dura el clima. None lo apaga.
    public void SetWeather(WeatherKind weather)
    {
        if (weatherOverlay == null) return;
        Color color;
        switch (weather)
        {
            case WeatherKind.Rain: color = new Color(0.2f, 0.35f, 0.7f, 0.22f); break;
            case WeatherKind.Sun: color = new Color(1f, 0.85f, 0.4f, 0.18f); break;
            case WeatherKind.Sandstorm: color = new Color(0.8f, 0.65f, 0.3f, 0.25f); break;
            case WeatherKind.Hail: color = new Color(0.75f, 0.9f, 1f, 0.22f); break;
            default:
                weatherOverlay.gameObject.SetActive(false);
                return;
        }
        weatherOverlay.color = color;
        weatherOverlay.gameObject.SetActive(true);
    }

    public void SetTerrain(FieldTerrain terrain)
    {
        if (terrainOverlay == null) return;
        Color color;
        switch (terrain)
        {
            case FieldTerrain.Electric: color = new Color(1f, 0.95f, 0.3f, 0.12f); break;
            case FieldTerrain.Psychic: color = new Color(0.9f, 0.4f, 0.8f, 0.12f); break;
            case FieldTerrain.Grassy: color = new Color(0.4f, 0.85f, 0.35f, 0.12f); break;
            case FieldTerrain.Misty: color = new Color(0.95f, 0.75f, 0.9f, 0.15f); break;
            default:
                terrainOverlay.gameObject.SetActive(false);
                return;
        }
        terrainOverlay.color = color;
        terrainOverlay.gameObject.SetActive(true);
    }

    public void ClearFieldOverlays()
    {
        SetWeather(WeatherKind.None);
        SetTerrain(FieldTerrain.None);
    }

    // ---------- animaciones de ataques ----------

    // Color clasico de cada tipo (tiñe proyectiles, impactos y pulsos de estado)
    public static Color ColorForType(PokenType type)
    {
        switch (type)
        {
            case PokenType.Fire: return new Color(1f, 0.45f, 0.2f);
            case PokenType.Water: return new Color(0.3f, 0.6f, 1f);
            case PokenType.Grass: return new Color(0.45f, 0.85f, 0.35f);
            case PokenType.Electric: return new Color(1f, 0.9f, 0.25f);
            case PokenType.Ice: return new Color(0.6f, 0.9f, 1f);
            case PokenType.Fighting: return new Color(0.8f, 0.25f, 0.2f);
            case PokenType.Poison: return new Color(0.7f, 0.35f, 0.8f);
            case PokenType.Ground: return new Color(0.85f, 0.7f, 0.4f);
            case PokenType.Flying: return new Color(0.65f, 0.75f, 1f);
            case PokenType.Psychic: return new Color(1f, 0.4f, 0.65f);
            case PokenType.Bug: return new Color(0.65f, 0.75f, 0.2f);
            case PokenType.Rock: return new Color(0.75f, 0.65f, 0.4f);
            case PokenType.Ghost: return new Color(0.45f, 0.35f, 0.65f);
            case PokenType.Dragon: return new Color(0.45f, 0.35f, 0.95f);
            case PokenType.Dark: return new Color(0.45f, 0.4f, 0.4f);
            case PokenType.Steel: return new Color(0.7f, 0.7f, 0.8f);
            case PokenType.Fairy: return new Color(1f, 0.65f, 0.8f);
            default: return new Color(0.85f, 0.85f, 0.8f); // Normal / None
        }
    }

    // Animacion completa de un ataque: anticipacion -> viaje/embestida ->
    // impacto -> recuperacion. Con animationOverride usa los tiempos y sprites
    // del asset; sin el, la generica: fisico embiste, especial dispara un
    // proyectil del color del tipo, estado destella sobre el usuario.
    // El drenado de HP es el evento SIGUIENTE, asi que el daño visual nunca
    // ocurre antes del impacto.
    public IEnumerator PlayAttack(PokenView attacker, PokenView defender, MoveDefinition move)
    {
        MoveAnimationDefinition anim = move != null ? move.animationOverride : null;
        // Solo ~1 de cada 3 movimientos tiene hoja propia en el pack (los
        // vistosos). Para el resto usamos la generica de su tipo, asi un
        // Ascuas o un Placaje tambien muestran algo en vez del destello pelado.
        if (anim == null && move != null) anim = TypeFallback(move.type);
        bool isStatus = move != null && (move.attackType == AttackType.Status || move.power <= 0);
        bool isSpecial = move != null && move.attackType == AttackType.Special;
        PokenType type = move != null ? move.type : PokenType.None;

        Color color = (anim != null && anim.tint.a > 0f) ? anim.tint : ColorForType(type);
        float windup = anim != null ? anim.windupTime : 0.15f;
        float travel = anim != null ? anim.travelTime : (isSpecial ? 0.28f : 0f);
        float impact = anim != null ? anim.impactTime : 0.20f;
        float recovery = anim != null ? anim.recoveryTime : 0.12f;
        float shake = anim != null ? anim.cameraShake : 0.05f;
        bool flash = anim != null && anim.screenFlash;
        // TODO sonidos: anim.windupSound / anim.impactSound via AudioService cuando haya sfx

        // Movimientos de estado: el efecto va sobre el USUARIO, no sobre el
        // rival, y sin sacudida ni parpadeo (no esta recibiendo un golpe).
        // Sin secuencia asignada, el pulso de color de siempre.
        if (isStatus)
        {
            bool tieneSecuencia = anim != null && anim.impactFrames != null && anim.impactFrames.Length > 0;
            if (tieneSecuencia)
                yield return ImpactBurst(attacker, color, impact, 0f, false, anim, false);
            else
                yield return PulseTint(attacker, color, 2);
            yield break;
        }

        Vector3 attackerHome = attacker.transform.position;
        Vector3 impactPoint = defender.transform.position + Vector3.up * 0.45f;
        Vector3 towardDefender = (defender.transform.position - attackerHome).normalized;

        // 1) anticipacion: pequeño retroceso de carga
        yield return MoveOver(attacker.transform, attackerHome,
            attackerHome - towardDefender * 0.18f, windup);

        if (isSpecial && travel > 0f)
        {
            // 2a) especial: paso al frente + proyectil hasta el objetivo
            yield return MoveOver(attacker.transform, attacker.transform.position,
                attackerHome + towardDefender * 0.12f, 0.06f);

            if (projectileRenderer != null)
            {
                Sprite[] travelFrames = anim != null ? anim.projectileFrames : null;
                bool animatedTravel = travelFrames != null && travelFrames.Length > 0;
                Vector3 projectileHomeScale = projectileRenderer.transform.localScale;

                if (animatedTravel)
                {
                    // arte a todo color: no lo teñimos ni le imponemos el color del tipo
                    projectileRenderer.sprite = travelFrames[0];
                    projectileRenderer.color = Color.white;
                    if (anim.projectileScale > 0f)
                        projectileRenderer.transform.localScale = projectileHomeScale * anim.projectileScale;
                }
                else
                {
                    if (anim != null && anim.projectileSprite != null)
                        projectileRenderer.sprite = anim.projectileSprite;
                    projectileRenderer.color = color;
                }

                Vector3 origin = attackerHome + towardDefender * 0.3f + Vector3.up * 0.5f;
                projectileRenderer.gameObject.SetActive(true);
                if (animatedTravel)
                {
                    yield return MoveOverAnimated(projectileRenderer, origin, impactPoint, travel,
                        travelFrames, anim.projectileFps);
                }
                else
                {
                    yield return MoveOver(projectileRenderer.transform, origin, impactPoint, travel);
                }
                projectileRenderer.gameObject.SetActive(false);
                projectileRenderer.transform.localScale = projectileHomeScale;
            }
            else
            {
                yield return new WaitForSeconds(travel);
            }
        }
        else
        {
            // 2b) fisico: embestida hasta el objetivo
            Vector3 lungeTo = Vector3.Lerp(attackerHome, defender.transform.position, 0.55f);
            yield return MoveOver(attacker.transform, attacker.transform.position, lungeTo, 0.16f);
        }

        // 3) impacto: destello (o secuencia) + parpadeo del defensor + sacudida de camara
        yield return ImpactBurst(defender, color, impact, shake, flash, anim);

        // 4) recuperacion: el atacante vuelve a su lugar
        yield return MoveOver(attacker.transform, attacker.transform.position, attackerHome, recovery + 0.06f);
        attacker.transform.position = attackerHome;
    }

    private IEnumerator ImpactBurst(PokenView defender, Color color,
        float duration, float shakeStrength, bool screenFlash,
        MoveAnimationDefinition anim = null, bool flickerTarget = true)
    {
        // Con una secuencia asignada el impacto es una animacion cuadro a cuadro;
        // si no, el destello estatico de siempre.
        Sprite[] frames = anim != null ? anim.impactFrames : null;
        bool hasFrames = frames != null && frames.Length > 0;
        float frameStep = 0f;
        Vector3 impactHomeScale = Vector3.one;

        if (impactRenderer != null)
        {
            impactRenderer.transform.position = defender.transform.position + Vector3.up * 0.45f;
            impactHomeScale = impactRenderer.transform.localScale;

            if (hasFrames)
            {
                float fps = Mathf.Max(1f, anim.impactFps);
                frameStep = 1f / fps;
                // los frames mandan sobre impactTime: la animacion se ve entera
                duration = frames.Length * frameStep;

                impactRenderer.sprite = frames[0];
                impactRenderer.color = anim.tintImpactFrames
                    ? new Color(color.r, color.g, color.b, 1f)
                    : Color.white;
                if (anim.impactScale > 0f)
                    impactRenderer.transform.localScale = impactHomeScale * anim.impactScale;
            }
            else
            {
                if (anim != null && anim.impactSprite != null)
                    impactRenderer.sprite = anim.impactSprite;
                impactRenderer.color = new Color(color.r, color.g, color.b, 1f);
            }

            impactRenderer.gameObject.SetActive(true);
        }
        if (screenFlash && fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.color = new Color(1f, 1f, 1f, 0.45f);
        }

        Camera cam = Cam;
        Vector3 camHome = cam != null ? cam.transform.position : Vector3.zero;
        SpriteRenderer defenderRenderer = defender.Renderer;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // avance de la secuencia de impacto
            if (hasFrames && impactRenderer != null)
            {
                int index = Mathf.Clamp((int)(t / frameStep), 0, frames.Length - 1);
                impactRenderer.sprite = frames[index];
            }
            // parpadeo del golpeado
            if (flickerTarget && defenderRenderer != null)
                defenderRenderer.enabled = ((int)(t / 0.05f)) % 2 == 0;
            // sacudida de camara
            if (cam != null && shakeStrength > 0f)
                cam.transform.position = camHome + (Vector3)(Random.insideUnitCircle * shakeStrength);
            // el flash de pantalla se apaga gradualmente
            if (screenFlash && fadeOverlay != null)
                fadeOverlay.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 0f, t / duration));
            yield return null;
        }

        if (defenderRenderer != null) defenderRenderer.enabled = true;
        if (cam != null) cam.transform.position = camHome;
        if (impactRenderer != null)
        {
            impactRenderer.gameObject.SetActive(false);
            // la escala es compartida: si no se restaura, el proximo ataque la hereda
            impactRenderer.transform.localScale = impactHomeScale;
        }
        if (screenFlash && fadeOverlay != null)
        {
            fadeOverlay.color = new Color(1f, 1f, 1f, 0f);
            fadeOverlay.gameObject.SetActive(false);
        }
    }

    // ---------- animacion generica por tipo ----------

    // Las genericas las arma PokeRush > Importar animaciones de ataque, una por
    // tipo, copiando los frames de un movimiento representativo. Se cachean
    // porque PlayAttack corre en cada ataque del combate.
    private static Dictionary<PokenType, MoveAnimationDefinition> typeFallbacks;

    private static MoveAnimationDefinition TypeFallback(PokenType type)
    {
        if (type == PokenType.None) return null;

        if (typeFallbacks == null)
        {
            typeFallbacks = new Dictionary<PokenType, MoveAnimationDefinition>();
            foreach (MoveAnimationDefinition generic in
                     Resources.LoadAll<MoveAnimationDefinition>("Definitions/MoveAnimations/PorTipo"))
            {
                if (System.Enum.TryParse(generic.name, out PokenType parsed))
                    typeFallbacks[parsed] = generic;
            }
        }

        MoveAnimationDefinition result;
        return typeFallbacks.TryGetValue(type, out result) ? result : null;
    }

    // ---------- utilidades ----------

    // Igual que MoveOver pero ademas cicla una secuencia de sprites durante el
    // trayecto: el chorro de un Lanzallamas se ve moviendose Y ardiendo.
    // Los frames se repiten en loop, asi una tira corta cubre viajes largos.
    private static IEnumerator MoveOverAnimated(SpriteRenderer renderer, Vector3 from, Vector3 to,
        float duration, Sprite[] frames, float fps)
    {
        float step = 1f / Mathf.Max(1f, fps);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            renderer.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            renderer.sprite = frames[(int)(t / step) % frames.Length];
            yield return null;
        }
        renderer.transform.position = to;
    }

    private static IEnumerator MoveOver(Transform target, Vector3 from, Vector3 to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            target.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        target.position = to;
    }

    private static void ResetRendererColor(PokenView pokenView)
    {
        SpriteRenderer renderer = pokenView.Renderer;
        if (renderer != null) renderer.color = Color.white;
    }
}
