using UnityEngine;

// Le da el "cada frame" a AudioService, que es una clase comun: los fundidos
// de la musica, el volumen que baja durante un jingle y los efectos largos que
// se apagan de a poco. Lo agrega AudioService.Initialize en el objeto del
// GameManager (que sobrevive los cambios de escena); no se pone a mano.
public class AudioServiceRunner : MonoBehaviour
{
    private void Update() => AudioService.Instance.Tick(Time.unscaledDeltaTime);
}
