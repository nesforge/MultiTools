# Revisión previa a publicación · MiniTools 1.4.2

Fecha de revisión: 2026-10-12. Alcance: código, configuración de build, archivos
candidatos a Git, todos los objetos locales de Git, recursos activos y ejecución
Windows en este equipo. No se realizó un pentest externo ni una certificación.

## Hallazgos y correcciones

| Ubicación | Hallazgo / medida |
| --- | --- |
| `.git/`, índice y refs | Índice inicialmente vacío, cero commits alcanzables, sin remoto. Había 209 objetos, incluidos 146 blobs; se examinaron por patrones sin imprimir valores. No hay historial alcanzable que purgar. No se borró ni reescribió Git. |
| Reportes `*Results.txt`, `TestResults/`, `dist/`, `.dotnet-home/`, `**/bin`, `**/obj` | Reportes pueden contener rutas de usuario, estados de ventanas o entradas sintéticas; binarios, respaldos y cachés no son código de publicación. Excluidos y conservados en disco. |
| Documentos históricos de `docs/`, `docs/internal/`, `assets/briocraft/` y referencias gráficas antiguas | Material interno, inventario de complementos y recursos sin uso en la app. Excluidos; README público y documentación nueva describen el estado actual. |
| `Configuration/SettingsStore.cs`, `ToolPreferences.cs`, `ConfigurationJson.cs` | Lectura sin límite sustituida por stream limitado a 64 KiB/profundidad 8. Errores de configuración sin rutas del sistema. Compatibilidad y recuperación probadas. |
| `UI/RecorderPage.cs`, README y SECURITY | Se eliminó la afirmación equívoca de que no se guarda texto. Los códigos de teclas pueden reconstruir lo escrito; se explica el riesgo de datos privados y el almacenamiento sin cifrado. |
| `global.json`, `Directory.Build.props`, `NuGet.Config`, bloqueos y scripts | SDK/runtime fijados, feed oficial, restauración bloqueada, analizadores y advertencias como errores. No se agregaron dependencias de terceros. |
| `.github/workflows/ci.yml` | Acciones oficiales fijadas por SHA, `contents: read`, checkout sin credenciales persistentes, sin secretos ni publicación automática. |

No se encontraron credenciales reales con los patrones utilizados. No fue
necesario revocar/rotar una credencial identificada. El procedimiento si aparece
una está en SECURITY.md: rotar primero y acordar el plan antes de purgar historial.

El control `scripts/check-publication.ps1` detecta tokens conocidos, claves
privadas, asignaciones sospechosas y rutas privadas, y rechaza material generado o
ignorado que se intente incluir. Solo reporta ubicación/tipo, nunca el valor.
No es un detector universal de secretos: puede omitir formatos nuevos o datos
personales sin patrón. Los binarios gráficos se revisan visualmente por separado.

## Fronteras de confianza

- Entradas reales: controles UI, ajustes locales, JSON importados/biblioteca y eventos Win32 durante grabación explícita.
- Importaciones: versión/campos/duplicados/enums/rangos/tamaño/profundidad validados; no se interpretan scripts, no se lanzan comandos ni se ejecuta al importar.
- Permisos: manifiesto `asInvoker`; cuenta y sesión de Windows son la frontera de acceso. No existe acceso remoto, login, autorización de servidor, HTML, XSS, cargas web ni secretos que lleguen a un navegador. No se añadieron controles web irrelevantes.
- Datos: código de tecla, posición, tiempos y nombre de secuencia. Pueden ser sensibles, permanecen locales y sin cifrar. No publicar macros personales; solo ejemplos de esperas y capturas del arnés con datos ficticios.
- Ejecución: exclusión entre herramientas, esperas cancelables, liberación de entradas y hooks al parar/cerrar. Una macro puede operar sobre otras aplicaciones: revisar sus pasos antes de ejecutarla.

## Dependencias y recursos

La auditoría `dotnet package list --no-restore --vulnerable --include-transitive`
se ejecutó sobre ambos proyectos contra nuget.org y no reportó paquetes
vulnerables. El programa usa .NET/WinForms y tooling implícito
`Microsoft.NET.ILLink.Tasks` 10.0.12. Este resultado no certifica el runtime ni
sustituye su revisión de avisos oficiales antes de cada release.

El icono activo pesa 16.897 bytes y la referencia de logo para pruebas 7.209.
El PNG histórico de 752.283 bytes, el icono viejo y el kit corporativo no se
incrustan; tampoco se incluyen en los fuentes públicos. Se conserva compresión
single-file y presupuesto x64 de 60 MB. No se hizo una actualización mayor ni
se sacrificaron funciones para bajar tamaño.

| Binario | Bytes | MB decimales |
| --- | ---: | ---: |
| MiniTools 1.4.1 anterior | 51.707.585 | 51,71 |
| MiniTools 1.4.2 revisado | 51.707.710 | 51,71 |

La exportación pública contiene 65 archivos y excluye los recursos sin uso,
reportes, cachés e historial interno. La anterior entrega de fuentes pesaba
1.296.789 bytes; el ZIP público revisado ronda 174 KB (aproximadamente 86,6 % menos).
La reducción es del paquete de fuentes, conservando todas las funciones.

El aumento de 125 bytes corresponde al cambio de código/metadatos; no se afirma
una reducción del .exe en esta revisión. Se preservó el anterior en un respaldo
local excluido. El empaquetado incluye el runtime y no requiere instalaciones
para el usuario final.

## Comprobaciones

Se ejecutaron restauración bloqueada, compilación Release con analizadores
(0 advertencias/0 errores), auditoría NuGet y tres grupos de pruebas sin escritorio:
configuración hostil, AutoClick y automatización/almacenamiento. El control de
archivos públicos pasa sin coincidencias. También pasó el test del control con
token ficticio, contenido del índice distinto al working tree y .env ignorado pero
rastreado.
`dotnet format analyzers --verify-no-changes --no-restore --severity warn` terminó
sin cambios requeridos, y todos los scripts PowerShell tienen sintaxis válida.

La suite interactiva terminó con 17 comprobaciones PASS y cero fallos: ambos lados,
0,1/1/10/100 clics/s, selector y teclado, captura/cancelación/colisión de atajo,
alternancia global sin repetición, contador, grabador, editor, pausa, parada global
con Ctrl retenido y liberación al cerrar. El smoke del .exe aislado pasó sus seis
comprobaciones, incluyendo startup detenido y cierre durante actividad.
Las ejecuciones iniciales revelaron esperas fijas insuficientes para la cola de
entrada/UI de Windows. Las pruebas ahora esperan, con límites, el estado real y
la tecla liberada; no se desactivó ninguna aserción ni se cambió el motor.
Se inspeccionaron capturas de las tres páginas con datos ficticios, teclado,
foco, controles y estado al tamaño mínimo. Los PNG publicados son renders solo
de la ventana de prueba, sin el escritorio ni datos del usuario.
Se restauró desde una copia limpia de fuentes con caché de paquetes nueva y
modo CI bloqueado; compilación, tres grupos de pruebas de lógica y build pasaron.
Ese .exe tuvo el mismo tamaño y SHA256 que el original revisado en este equipo.
Esto no verifica todos los sistemas/arquitecturas ni la ejecución hospedada de CI.
SHA256 del ejecutable revisado: `6250F74314B2CCC8F2C1B733C02B25A3D4D3B6B37FA9B0A978254984587BEBE6`.

## Pendientes y límites

- Primera ejecución real de la CI en GitHub: requiere publicar con autorización.
- Windows limpio sin SDK: no se dispone de otro equipo/VM. El smoke aísla el .exe y bloquea el descubrimiento del runtime instalado, pero el host sí tiene herramientas de desarrollo.
- ARM64/x86, lectores de pantalla y matriz de DPI/múltiples monitores: no verificados en equipos reales.
- Móvil: no aplica; el producto es WinForms para Windows, no una página responsive. Se revisan sus tres herramientas en escritorio y tamaño mínimo.
- Biblioteca: `AutomationStore.ListMacros` lee sincrónicamente hasta 1.000 documentos de hasta 16 MiB y conserva sus pasos. Una biblioteca extremadamente grande puede consumir memoria o retrasar el inicio; no se ha probado ese peor caso. La futura carga perezosa requiere una revisión aparte.
- Credenciales sin patrones reconocibles, archivos cifrados y derechos de recursos aportados por el propietario: necesitan revisión humana.
- Decisiones de licencia, propietario, nombre, visibilidad y firma del binario: ver `PUBLICACION.md`.

## Skills y complementos

Se leyeron y aplicaron `agent-skills:security-and-hardening`,
`agent-skills:code-review-and-quality`, `agent-skills:git-workflow-and-versioning`
y `agent-skills:shipping-and-launch`, junto con `ecc:github-ops` para preparar la CI
y separar acciones locales de una futura publicación autorizada.
`codebase-memory` se leyó; sus herramientas no están disponibles en esta sesión,
por lo que se verificó la estructura en el código fuente. Se revisó la instrucción
AGENTS.md proporcionada; no había otro archivo AGENTS.md en el árbol o padres comprobados.

Se comprobó el inventario local de complementos habilitados. Se usaron las pautas
de agent-skills y ecc; los demás no aportaban una capacidad necesaria. El plugin
Code Review disponible expone diagnósticos de CI de PR, y aquí no hay PR ni remoto.
codex-app-tools se utilizó para solicitar la vista del diff preparado.
No se instalaron plugins, no se contactaron otras cuentas ni se publicó nada.


## Diff preparado para revisión

Git no tenía commits ni archivos rastreados: el diff del índice presenta la primera
incorporación de 65 archivos públicos, incluyendo el código existente conservado.
Los cambios de esta tarea en código se limitan a la lectura de configuración,
mensaje de privacidad, versión y bloqueos/RIDs. Las pruebas añaden los límites y
fortalecen la sincronización de entradas nativas. El resto es documentación y
tooling de publicación. Usa git diff --cached y el informe de PUBLICACION.md;
no se ha creado el primer commit ni se ha agregado un remoto.
