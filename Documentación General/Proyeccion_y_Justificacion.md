# Proyección y justificación del proyecto

## Proyección del proyecto

PokeRush se proyecta como un RPG 2D desarrollado en Unity y C#, con una estructura modular basada en MVC. El alcance inicial del proyecto incluye exploración de mapa, interacción con NPCs, eventos de historia, combates por turnos, sistema de inventario, progresión de equipo y enfrentamientos contra bosses.

El juego utiliza criaturas propias llamadas Poken. Cada Poken cuenta con estadísticas, puntos de salud, movimientos, tipos, experiencia, niveles y posibles cosméticos. La información de especies, movimientos, enemigos, bosses, objetos, personajes y eventos se organiza mediante ScriptableObjects, lo que permite agregar contenido sin modificar directamente la lógica principal del sistema.

La progresión del jugador se apoya en un sistema de guardado y carga con SQLite local. El estado persistente incluye datos del jugador, dinero, equipo, movimientos, inventario, cosméticos equipados y flags de historia. Esto permite conservar el avance de la partida y reconstruir el estado del juego al continuar.

La estructura de gameplay se divide en los siguientes módulos principales:

- Movimiento y exploración del personaje en escenarios 2D.
- Eventos de mapa controlados por condiciones y flags de historia.
- Diálogos con NPCs.
- Combates por turnos contra bosses.
- Sistema de equipo de Poken.
- Inventario con objetos de curación, batalla y especiales.
- Tienda y economía interna.
- Cosméticos o skins para personalización.
- Guardado y carga de progreso.
- Menú de configuración para audio y dificultad.

El proyecto también contempla una base para cinemáticas y eventos especiales asociados a bosses. Actualmente, los bosses se definen mediante `BossDefinition`, mientras que los eventos se administran con `EventDefinition`, permitiendo vincular diálogos, cinemáticas, condiciones de historia y combates.

## Justificación del proyecto

El desarrollo de PokeRush se justifica como un proyecto integrador porque combina programación orientada a objetos, diseño de sistemas, persistencia de datos, interfaces de usuario y diseño de gameplay en un producto ejecutable.

Desde el punto de vista técnico, el proyecto permite aplicar una arquitectura por capas. Los modelos concentran el estado y la lógica del dominio, las vistas se encargan de la interfaz y la representación visual, los controladores coordinan la interacción entre sistemas y los servicios administran funcionalidades transversales como base de datos, guardado, audio y carga de escenas.

La elección de Unity se justifica por su soporte para desarrollo 2D, manejo de escenas, UI, Input System, animación, Tilemaps, URP 2D y organización de assets. C# permite implementar la lógica del juego con clases, enumeraciones, colecciones, herencia y composición, elementos necesarios para modelar entidades como jugador, Poken, movimientos, enemigos, inventario y eventos.

La base de datos SQLite se incorpora para resolver la persistencia del progreso del jugador. Su uso permite trabajar con tablas, claves, relaciones y reconstrucción de objetos a partir de datos guardados. En el proyecto se aplica sobre entidades como jugador, equipo, movimientos, inventario, cosméticos, diálogos y flags activos.

El sistema de bosses justifica el desarrollo de lógica de combate por turnos, control de fases, cálculo de daño, recompensas, derrota, victoria y dificultad. Además, la separación entre definiciones de contenido y lógica de ejecución facilita extender el juego con nuevos jefes, Poken, objetos o eventos.

El sistema de historia mediante flags permite representar decisiones, progreso y condiciones de activación. Esto sirve para controlar qué diálogos, eventos o combates se ejecutan según el estado actual de la partida.

En conjunto, PokeRush permite demostrar conocimientos técnicos de desarrollo de videojuegos, diseño de bases de datos, arquitectura de software, documentación, control de versiones y trabajo en equipo.

