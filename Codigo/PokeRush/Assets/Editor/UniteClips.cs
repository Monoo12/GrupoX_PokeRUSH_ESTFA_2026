using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Elige los clips de animacion que le corresponden a una especie.
//
// Hace falta porque varios FBX del pack traen animaciones de OTROS personajes
// adentro. El caso grave es Miraidon: de sus 74 clips, 41 son de Wigglytuff
// (id 0040) y de Mewtwo (id 0150). Como esos clips estan hechos para otro
// esqueleto, al reproducirlos sobre Miraidon mueven 9 de sus 144 huesos y el
// resto se queda en pose de bind: es la "animacion de ataque rota".
//
// No se borra nada: los clips son sub-assets del FBX y siguen ahi. Lo que se
// hace es no elegirlos nunca, filtrando por el id de 4 digitos que llevan tanto
// el nombre del modelo como el de cada clip.
//
// Afectados (7-ago-2026): Miraidon (41 ajenos), Goomy, metagross y Snorlax (1
// cada uno).
public static class UniteClips
{
    /// Id interno de 4 digitos de la especie, sacado del nombre del FBX. Los
    /// reference.fbx no lo llevan en el nombre, asi que cae a la malla.
    public static string IdDe(string rutaFbx, GameObject modelo)
    {
        Match porArchivo = Regex.Match(Path.GetFileName(rutaFbx), @"_bat_(\d{4})");
        if (porArchivo.Success) return porArchivo.Groups[1].Value;

        if (modelo != null)
        {
            var renderer = modelo.GetComponentInChildren<SkinnedMeshRenderer>();
            if (renderer != null && renderer.sharedMesh != null)
            {
                Match porMalla = Regex.Match(renderer.sharedMesh.name, @"(\d{4})");
                if (porMalla.Success) return porMalla.Groups[1].Value;
            }
        }
        return null;
    }

    /// Clips del FBX que son de esta especie. Un clip sin id en el nombre se
    /// considera propio: el pack no lo marca, pero tampoco es de otro.
    public static List<AnimationClip> PropiosDe(string rutaFbx)
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(rutaFbx);
        string id = IdDe(rutaFbx, modelo);
        var propios = new List<AnimationClip>();

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(rutaFbx))
        {
            var clip = asset as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview")) continue;

            Match delClip = Regex.Match(clip.name, @"_bat_(\d{4})");
            if (id != null && delClip.Success && delClip.Groups[1].Value != id) continue;

            propios.Add(clip);
        }
        return propios;
    }

    /// Primer clip cuyo nombre empieza con el prefijo. Se compara con el guion
    /// bajo incluido ("ani_idle_") para que ani_idle2 no le gane al bueno.
    public static AnimationClip Buscar(List<AnimationClip> clips, string prefijo)
    {
        foreach (AnimationClip clip in clips)
            if (clip.name.StartsWith(prefijo)) return clip;

        return null;
    }
}
