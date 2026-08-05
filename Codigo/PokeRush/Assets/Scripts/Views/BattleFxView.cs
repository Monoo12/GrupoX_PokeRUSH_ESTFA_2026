using System.Collections;
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
        pokenView.Show();

        Vector3 basePos = pokenView.transform.position;
        for (float t = 0f; t < appearDuration * 1.6f; t += Time.deltaTime)
        {
            float k = t / (appearDuration * 1.6f);
            pokenView.SetTint(new Color(1f, 1f, 1f, k));
            pokenView.transform.position = basePos + Vector3.up * Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * 0.15f;
            yield return null;
        }
        pokenView.SetTint(Color.white);
        pokenView.transform.position = basePos;
    }

    private IEnumerator AppearGrow(PokenView pokenView)
    {
        Vector3 fullScale = pokenView.transform.localScale;
        pokenView.transform.localScale = Vector3.zero;
        pokenView.Show();
        pokenView.SetTint(Color.white);

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
        Vector3 basePos = pokenView.transform.position;

        for (float t = 0f; t < faintDuration; t += Time.deltaTime)
        {
            float k = t / faintDuration;
            pokenView.transform.position = basePos + Vector3.down * k * 0.9f;
            pokenView.SetTint(new Color(1f, 1f, 1f, 1f - k));
            yield return null;
        }

        pokenView.Hide();
        pokenView.transform.position = basePos;
        pokenView.SetTint(Color.white);
    }

    // ---------- pulsos de color (stats, estados, curacion) ----------

    public IEnumerator PulseTint(PokenView pokenView, Color color, int pulses = 2)
    {
        if (pokenView == null) yield break;

        const float pulseTime = 0.16f;
        for (int i = 0; i < pulses; i++)
        {
            for (float t = 0f; t < pulseTime; t += Time.deltaTime)
            {
                pokenView.SetTint(Color.Lerp(Color.white, color, t / pulseTime));
                yield return null;
            }
            for (float t = 0f; t < pulseTime; t += Time.deltaTime)
            {
                pokenView.SetTint(Color.Lerp(color, Color.white, t / pulseTime));
                yield return null;
            }
        }
        pokenView.SetTint(Color.white);
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

        // Movimientos de estado: destello del color del tipo sobre el usuario
        if (isStatus)
        {
            yield return PulseTint(attacker, color, 2);
            yield break;
        }

        Vector3 attackerHome = attacker.transform.position;
        // El impacto apunta al cuerpo del defensor, no a sus pies. La altura sale
        // del visual, asi que le pega bien tanto a un Diglett como a un Onix (con
        // sprites todos median 1 unidad, con modelos 3D no).
        Vector3 impactPoint = defender.HitPoint;
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
                if (anim != null && anim.projectileSprite != null)
                    projectileRenderer.sprite = anim.projectileSprite;
                projectileRenderer.color = color;
                Vector3 origin = attackerHome + towardDefender * 0.3f
                                 + Vector3.up * (attacker.Height * 0.5f);
                projectileRenderer.gameObject.SetActive(true);
                yield return MoveOver(projectileRenderer.transform, origin, impactPoint, travel);
                projectileRenderer.gameObject.SetActive(false);
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

        // 3) impacto: destello + parpadeo del defensor + sacudida de camara
        yield return ImpactBurst(defender, color, impact, shake, flash);

        // 4) recuperacion: el atacante vuelve a su lugar
        yield return MoveOver(attacker.transform, attacker.transform.position, attackerHome, recovery + 0.06f);
        attacker.transform.position = attackerHome;
    }

    private IEnumerator ImpactBurst(PokenView defender, Color color,
        float duration, float shakeStrength, bool screenFlash)
    {
        if (impactRenderer != null)
        {
            impactRenderer.transform.position = defender.HitPoint;
            impactRenderer.color = new Color(color.r, color.g, color.b, 1f);
            impactRenderer.gameObject.SetActive(true);
        }
        if (screenFlash && fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.color = new Color(1f, 1f, 1f, 0.45f);
        }

        Camera cam = Camera.main;
        Vector3 camHome = cam != null ? cam.transform.position : Vector3.zero;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            // parpadeo del golpeado
            defender.SetRendererVisible(((int)(t / 0.05f)) % 2 == 0);
            // sacudida de camara: el desplazamiento se rota con la camara para que
            // la sacudida sea siempre en el plano de la pantalla. En 3D la camara
            // esta picada y rotada, y un offset en XY del mundo se veria torcido.
            if (cam != null && shakeStrength > 0f)
            {
                Vector3 jitter = (Vector3)(Random.insideUnitCircle * shakeStrength);
                cam.transform.position = camHome + cam.transform.rotation * jitter;
            }
            // el flash de pantalla se apaga gradualmente
            if (screenFlash && fadeOverlay != null)
                fadeOverlay.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.45f, 0f, t / duration));
            yield return null;
        }

        defender.SetRendererVisible(true);
        if (cam != null) cam.transform.position = camHome;
        if (impactRenderer != null) impactRenderer.gameObject.SetActive(false);
        if (screenFlash && fadeOverlay != null)
        {
            fadeOverlay.color = new Color(1f, 1f, 1f, 0f);
            fadeOverlay.gameObject.SetActive(false);
        }
    }

    // ---------- utilidades ----------

    private static IEnumerator MoveOver(Transform target, Vector3 from, Vector3 to, float duration)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            target.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        target.position = to;
    }
}
