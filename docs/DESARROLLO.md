# Desarrollo de MiniTools

## Módulos

| Carpeta | Responsabilidad |
| --- | --- |
| `src/ClickMoves/UI` | Formularios, páginas, selector vectorial, logo y estilos compartidos |
| `Configuration` | Ajustes, preferencias y lectura JSON acotada |
| `AutoClick` | Motor cancelable y contador; interfaz de envío sustituible en pruebas |
| `Automation` | Modelo validado, buffer del grabador, reproductor y exclusión de actividad |
| `Platform` | SendInput, hooks y atajos Win32; sin comandos de shell en el programa |
| `Storage` | Importación/exportación JSON y biblioteca local |
| `tests/ClickMoves.Tests` | Arnés de lógica, UI, entradas controladas y binario aislado |
| `assets` | Icono del producto y referencia de logo usada por las pruebas |

El namespace y la carpeta de datos `ClickMoves` se conservan por compatibilidad.
El producto, la ventana y el ejecutable se llaman MiniTools; BrioCraft es la
empresa desarrolladora. Los tokens visuales siguen en `UI/ToolStyle.cs` y la firma
editable en `UI/Branding.cs`. Azul del producto: `#1860B1`; fuente Segoe UI del sistema.

Los motores corren fuera del hilo UI y cancelan esperas al detener. El coordinador
reserva una sola herramienta incluso durante pausas y cuenta regresiva. Al cerrar
se detiene la actividad, se liberan las entradas enviadas y se desregistran hooks.
No agregues automatizaciones que eviten ese coordinador.

## Límites y datos

Los archivos importados son datos sin ejecución de scripts, rutas de comandos ni
URLs. Se validan versión, campos permitidos, duplicados, enums, coordenadas,
códigos de tecla, esperas, nombre y cantidad de pasos. La extensión JSON no se
considera prueba de validez. Una importación no inicia automáticamente una macro.

El Grabador conserva acciones en memoria hasta que se solicite guardar. Los JSON
se guardan sin cifrado, bajo los permisos del usuario. No añadas telemetría ni
servicios externos sin una decisión explícita y una revisión de privacidad.

## Actualizar SDK, runtime o dependencias

1. Revisar avisos oficiales y compatibilidad, incluyendo el runtime que viaja dentro del .exe.
2. Cambiar `global.json` y `Directory.Build.props` deliberadamente.
3. Ejecutar desde la raíz, con el SDK nuevo:

   ```powershell
   $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
   $env:DOTNET_CLI_HOME = Join-Path $PWD '.dotnet-home'
   $env:NUGET_PACKAGES = Join-Path $PWD '.dotnet-home/packages'
   dotnet restore tests/ClickMoves.Tests/ClickMoves.Tests.csproj --force-evaluate --use-lock-file -p:RestoreLockedMode=false
   git diff -- global.json Directory.Build.props src/ClickMoves/packages.lock.json tests/ClickMoves.Tests/packages.lock.json
   ./restore.ps1
   ./test.ps1 -CoreOnly
   ./build.ps1
   ./test.ps1
   ./test.ps1 -SmokeOnly
   ```

4. Auditar ambos proyectos con `dotnet package list --project RUTA.csproj --no-restore --vulnerable --include-transitive`.
5. Revisar y conservar los bloqueos generados; no editarlos a mano.

El bloqueo guarda integridad de paquetes NuGet. Los paquetes de runtime implícitos
se fijan mediante las versiones de SDK/runtime y los identificadores Windows del
proyecto. No hay paquetes de terceros ni scripts de instalación añadidos.

## Entrega y reversión

La CI de Windows ejecuta el control de archivos públicos, restore bloqueado,
compilación con analizadores y advertencias como errores, pruebas sin entradas al
escritorio y empaquetado. No publica releases, no necesita secretos, no usa
`pull_request_target` y desactiva credenciales persistentes del checkout. Las dos
acciones oficiales están fijadas por SHA verificado en sus tags; actualízalas
mediante revisión. Dependabot propone cambios mensuales, nunca se fusionan solos.

Antes de una release, ejecuta también la suite y smoke en un escritorio Windows
interactivo. Guarda el hash de la entrega y prueba un Windows sin SDK si se dispone
de él. Un rollback consiste en distribuir el .exe anterior verificado, conservando
primero una copia privada de las configuraciones/macros. No publiques esos datos
ni respaldos como código fuente. El código mantiene el formato JSON versión 1.
