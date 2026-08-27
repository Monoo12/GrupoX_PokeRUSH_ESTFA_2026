using System.Collections.Generic;
using UnityEngine;

// Crea/reusa filas dentro de un contenedor a partir de un prefab.
//
// Existe para que ninguna pantalla haga Instantiate/Destroy en cada refresco:
// destruir y recrear las filas en cada tecla hace parpadear el LayoutGroup y
// pierde la posicion del scroll.
public class WidgetPool<T> where T : MenuWidget
{
    private readonly T _prefab;
    private readonly Transform _container;
    private readonly List<T> _instances = new List<T>();

    public WidgetPool(T prefab, Transform container)
    {
        _prefab = prefab;
        _container = container;
    }

    public IReadOnlyList<T> Instances => _instances;

    public T Get(int index)
    {
        while (_instances.Count <= index)
        {
            T instancia = Object.Instantiate(_prefab, _container);
            instancia.name = _prefab.name + "_" + _instances.Count;
            _instances.Add(instancia);
        }
        T widget = _instances[index];
        widget.Index = index;
        widget.gameObject.SetActive(true);
        return widget;
    }

    // Apaga (no destruye) las filas sobrantes.
    public void HideFrom(int count)
    {
        for (int i = count; i < _instances.Count; i++)
            if (_instances[i] != null) _instances[i].gameObject.SetActive(false);
    }
}
