# Historias de Usuario y Criterios de Aceptación

---

# 1. Sistema de Combate y Táctica

###  Historia de Usuario
**Como** jugador,  
**quiero** poder elegir entre **4 movimientos distintos** en mi turno,  
**para** aplicar una estrategia específica contra el jefe.

**Criterio de Aceptación**
- **Dado que** es mi turno de combate,  
- **cuando** selecciono un movimiento del **Moveset**,  
- **entonces** se debe calcular el daño o efecto según la potencia y precisión, y restar los **Puntos de Salud (PS)** correspondientes al enemigo.

---

### Historia de Usuario
**Como** jugador,  
**quiero** utilizar objetos de curación durante el enfrentamiento,  
**para** evitar que mi Poker sea debilitado.

 **Criterio de Aceptación**
- **Dado que** tengo objetos disponibles en mi mochila,  
- **cuando** selecciono un **Objeto de Curación**,  
- **entonces** se deben restaurar los **PS** del Poker actual y consumir una unidad del inventario.

---

# 2. Progresión y Personalización

### Historia de Usuario
**Como** jugador,  
**quiero** que mi Poker evolucione al alcanzar un nivel determinado,  
**para** obtener mejores estadísticas y enfrentar jefes más difíciles.

**Criterio de Aceptación**
- **Dado que** mi Poker alcanzó el **nivel requerido**,  
- **cuando** finaliza el combate,  
- **entonces** se debe activar la secuencia de evolución y actualizar la ID del Pokémon a su forma de destino en la base de datos.

---

### Historia de Usuario
**Como** jugador,  
**quiero** equipar **Skins** a mis criaturas,  
**para** personalizar su apariencia visual en los escenarios 2D.

**Criterio de Aceptación**
- **Dado que** poseo una Skin en mi inventario,  
- **cuando** la selecciono en el menú de equipo,  
- **entonces** el sprite animado del Poker debe cambiar en Unity tanto en los menús como en la batalla.

---

# 3. Configuración y Ajustes

### Historia de Usuario
**Como** jugador,  
**quiero** acceder a un menú de ajustes,  
**para** configurar el volumen de la música y la dificultad del juego.

 **Criterio de Aceptación**
- **Dado que** estoy en el menú inicial,  
- **cuando** selecciono **"Ajustes"**,  
- **entonces** se deben mostrar opciones configurables que afecten:
  - Volumen global
  - Escala de dificultad de los 5 jefes

---

# 4. Administración y Desarrollo

### Historia de Usuario
**Como** desarrollador (**LevelUp Creators**),  
**quiero** que el código sea modular,  
**para** poder añadir nuevos jefes o Pokers sin reescribir la lógica base.

 **Criterio de Aceptación**
- **Dado que** se utiliza una arquitectura modular (ej.: **MVC**),  
- **cuando** se añade una nueva clase de objeto o enemigo,  
- **entonces** el sistema de combate debe integrarla automáticamente sin errores de lógica o colisiones.

---
