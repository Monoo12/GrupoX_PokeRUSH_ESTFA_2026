# Historias de Usuario — PokeRush

Cada historia sigue el formato **Como** *[rol]* **quiero** *[objetivo]* **para** *[beneficio]*,
y su **Confirmación** (3.ª C) se expresa como **criterios de aceptación** en formato
**Dado / Cuando / Entonces**. Las historias buscan cumplir el criterio **INVEST**
(Independientes, Negociables, Valiosas, Estimables, Pequeñas y Verificables).

> **Nota de terminología:** las criaturas del juego se denominan **Poken** (no "Pokémon" ni "Poker"),
> y sus puntos de vida son **PS** (Puntos de Salud).

---

## 1. Exploración del Mundo

### HU-01 · Mover al personaje por el mapa
**Como** jugador,
**quiero** mover a mi personaje por el escenario en 2D,
**para** explorar el mundo e interactuar con su contenido.

**Criterios de aceptación**
- **Dado que** estoy en una zona explorable, **cuando** uso los controles de movimiento, **entonces** el personaje se desplaza en la dirección indicada respetando las colisiones del mapa.

### HU-02 · Activar eventos al entrar a una zona
**Como** jugador,
**quiero** que ocurran eventos al ingresar a ciertas zonas,
**para** avanzar en la historia de forma natural.

**Criterios de aceptación**
- **Dado que** entro a una zona con un evento asociado, **cuando** se cumplen sus condiciones (flags requeridas), **entonces** se dispara el evento correspondiente (diálogo, cinemática o combate).
- **Dado que** un evento ya fue activado, **cuando** vuelvo a la zona, **entonces** no se repite.

---

## 2. Sistema de Combate

### HU-03 · Elegir movimientos en combate
**Como** jugador,
**quiero** elegir entre los **4 movimientos** de mi Poken en mi turno,
**para** aplicar una estrategia específica contra el enemigo.

**Criterios de aceptación**
- **Dado que** es mi turno de combate, **cuando** selecciono un movimiento del *moveset*, **entonces** se calcula el daño o efecto según su potencia, precisión y tipo, y se restan los **PS** correspondientes al enemigo.
- **Dado que** un movimiento se queda sin **PP**, **cuando** intento seleccionarlo, **entonces** el sistema no me permite usarlo.

### HU-04 · Usar objetos durante el combate
**Como** jugador,
**quiero** usar objetos de mi mochila durante el enfrentamiento,
**para** curar a mi Poken o cambiar el rumbo de la batalla.

**Criterios de aceptación**
- **Dado que** tengo objetos disponibles, **cuando** selecciono un objeto de curación, **entonces** se restauran los **PS** del Poken activo y se descuenta una unidad del inventario.
- **Dado que** uso un objeto, **cuando** termina la acción, **entonces** se consume mi turno y pasa el turno al enemigo.

### HU-05 · Enfrentar a los jefes (Boss Rush)
**Como** jugador,
**quiero** enfrentar a los jefes en combates por turnos,
**para** superar los desafíos principales del juego.

**Criterios de aceptación**
- **Dado que** inicia un combate contra un jefe, **cuando** comienza la fase de batalla, **entonces** se muestran los **PS** y el equipo del jefe en el HUD.
- **Dado que** el equipo del jefe queda sin Pokens con vida, **cuando** termina el último turno, **entonces** la batalla finaliza con victoria del jugador y se otorgan las recompensas.
- **Dado que** mi equipo queda sin Pokens con vida, **cuando** termina el turno, **entonces** la batalla finaliza con derrota.

---

## 3. Equipo y Progresión

### HU-06 · Ganar experiencia y subir de nivel
**Como** jugador,
**quiero** que mis Pokens ganen experiencia al combatir,
**para** que suban de nivel y mejoren sus estadísticas.

**Criterios de aceptación**
- **Dado que** gané un combate, **cuando** se reparte la experiencia, **entonces** se suma la **XP** a cada Poken participante.
- **Dado que** un Poken acumula la **XP** necesaria, **cuando** alcanza el umbral de su tabla de experiencia, **entonces** sube de nivel y se actualizan sus estadísticas.

### HU-07 · Organizar el equipo
**Como** jugador,
**quiero** ordenar los Pokens de mi equipo,
**para** decidir cuál entra primero al combate.

**Criterios de aceptación**
- **Dado que** estoy en el menú de equipo, **cuando** cambio la posición de un Poken, **entonces** se actualiza su orden (slot) y se conserva al guardar la partida.

---

## 4. Inventario y Economía

### HU-08 · Gestionar el inventario
**Como** jugador,
**quiero** consultar y usar los objetos de mi mochila,
**para** administrar mis recursos fuera del combate.

**Criterios de aceptación**
- **Dado que** abro el inventario, **cuando** se muestra, **entonces** veo mis objetos agrupados por tipo (curación, batalla, especiales) con su cantidad.
- **Dado que** uso un objeto fuera de combate, **cuando** confirmo la acción, **entonces** se aplica su efecto y se actualiza la cantidad.

### HU-09 · Comprar y vender en la tienda
**Como** jugador,
**quiero** comprar y vender objetos con un vendedor,
**para** conseguir lo que necesito gastando mi dinero.

**Criterios de aceptación**
- **Dado que** estoy en la tienda y tengo dinero suficiente, **cuando** compro un objeto, **entonces** se descuenta su precio de mi dinero y el objeto se agrega al inventario.
- **Dado que** no tengo dinero suficiente, **cuando** intento comprar, **entonces** el sistema no permite la compra y me avisa.

---

## 5. NPCs, Historia y Servicios

### HU-10 · Conversar con NPCs
**Como** jugador,
**quiero** hablar con los NPCs del mundo,
**para** recibir información y desbloquear avances de la historia.

**Criterios de aceptación**
- **Dado que** interactúo con un NPC, **cuando** inicia el diálogo, **entonces** se muestran sus líneas en orden.
- **Dado que** una línea otorga una flag de progreso, **cuando** finaliza el diálogo, **entonces** la flag queda registrada para habilitar nuevos eventos.

### HU-11 · Curar el equipo con la enfermera
**Como** jugador,
**quiero** acudir a la enfermera,
**para** recuperar la salud de todo mi equipo entre combates.

**Criterios de aceptación**
- **Dado que** hablo con la enfermera, **cuando** confirmo la curación, **entonces** se restauran los **PS** y **PP** de todos los Pokens del equipo a su valor máximo.

---

## 6. Personalización

### HU-12 · Equipar cosméticos (skins)
**Como** jugador,
**quiero** equipar skins a mis Pokens,
**para** personalizar su apariencia en los menús y en la batalla.

**Criterios de aceptación**
- **Dado que** poseo una skin, **cuando** la selecciono en el menú de equipo, **entonces** el sprite del Poken cambia tanto en los menús como en combate.
- **Dado que** equipé una skin, **cuando** guardo la partida, **entonces** la skin equipada se conserva al volver a jugar.

---

## 7. Partida y Configuración

### HU-13 · Guardar y cargar la partida
**Como** jugador,
**quiero** guardar mi progreso y retomarlo después,
**para** no perder lo avanzado.

**Criterios de aceptación**
- **Dado que** decido guardar, **cuando** confirmo, **entonces** se persisten en la base de datos mi jugador (nombre y dinero), equipo, inventario, cosméticos y flags de historia.
- **Dado que** abro una partida guardada, **cuando** se carga, **entonces** el estado del juego se restaura exactamente como lo dejé.

### HU-14 · Configurar el juego
**Como** jugador,
**quiero** acceder a un menú de ajustes,
**para** configurar el audio y la dificultad.

**Criterios de aceptación**
- **Dado que** estoy en el menú, **cuando** selecciono **Ajustes**, **entonces** puedo modificar el volumen global y la escala de dificultad de los jefes.
- **Dado que** cambio un ajuste, **cuando** vuelvo al juego, **entonces** el cambio se aplica de inmediato.

---

## 8. Desarrollo

### HU-15 · Código modular (equipo de desarrollo)
**Como** desarrollador del equipo,
**quiero** que el código siga una arquitectura modular (**MVC**),
**para** añadir nuevos jefes, Pokens u objetos sin reescribir la lógica base.

**Criterios de aceptación**
- **Dado que** se usa una arquitectura modular por capas (Model, View, Controller, Services, Data), **cuando** se agrega una nueva definición de enemigo u objeto (ScriptableObject), **entonces** el sistema la integra sin modificar la lógica de combate existente.
