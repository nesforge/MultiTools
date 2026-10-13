# Seguridad de MiniTools

## Reportar un problema

Para un reporte privado, contacta a **BrioCraft** mediante un mensaje directo a
[@briocrafttech en Instagram](https://www.instagram.com/briocrafttech/).
Es el contacto proporcionado por la desarrolladora; no se ha definido un correo
de seguridad ni un plazo de respuesta.

Envía la versión de MiniTools, versión de Windows, descripción del problema,
impacto y pasos mínimos de reproducción con datos ficticios. No envíes tokens,
contraseñas, grabaciones personales ni capturas con información privada.
Acuerda un canal privado adecuado antes de compartir material sensible.
Si el futuro repositorio habilita **Security → Report a vulnerability**, utiliza
esa opción. Evita publicar detalles explotables en un issue público.

## Alcance y datos locales

MiniTools es una aplicación de escritorio sin servidor, cuentas, navegador ni
telemetría de la aplicación. Corre con los permisos del usuario. Solo graba cuando
se activa el Grabador, pero los códigos de teclas pueden reconstruir contenido
escrito. No grabes contraseñas ni datos privados. Los JSON no están cifrados y
permanecen en el equipo hasta que el usuario los elimine. Importar un JSON no lo
ejecuta; revisa sus acciones antes de reproducirlo.

Configuración y biblioteca: `%LOCALAPPDATA%/ClickMoves/`. Grabaciones/exportaciones:
la ubicación elegida por el usuario. No compartas estas carpetas, respaldos o
reportes de prueba como parte del código fuente.

## Mantenimiento

La versión revisada es 1.4.2. No se promete soporte de seguridad para versiones
anteriores. Antes de cada entrega, revisa el SDK, runtime incluido, paquetes de
Microsoft y avisos de Windows; regenera y revisa los bloqueos solo al actualizar
dependencias deliberadamente. La auditoría NuGet no cubre por sí sola todos los
componentes del runtime ni las vulnerabilidades desconocidas.

Si aparece una credencial real en Git: revocarla/rotarla primero, revisar su uso,
identificar archivos y commits afectados y acordar un plan de eliminación del
historial con quienes usen el repositorio. Añadir `.gitignore` no elimina copias
rastreadas, commits anteriores ni forks. No reescribas historial sin coordinación.
