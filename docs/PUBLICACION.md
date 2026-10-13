# Preparación y publicación en GitHub

## Decisiones del propietario

- Cuenta u organización propietaria del repositorio: pendiente. Instagram no confirma un usuario de GitHub.
- Nombre del repositorio: pendiente; `MiniTools` es el nombre del producto, no un remoto configurado.
- Visibilidad pública o privada: pendiente.
- Licencia del código y permisos de recursos gráficos: pendiente. No había LICENSE y no se creó una por defecto.
- Correo de seguridad, responsable de seguimiento y activar reporte privado de vulnerabilidades: pendiente. Contacto inicial disponible: Instagram @briocrafttech.
- Firma de código del .exe y prueba en una instalación limpia de Windows: pendientes.

Opciones de licencia: conservar código sin licencia pública de reutilización,
licencia permisiva (por ejemplo MIT/Apache-2.0) o copyleft (por ejemplo GPL), después
de revisar derechos y objetivos. No se ha seleccionado ninguna. La publicación
pública permite ver y bifurcar según los términos de GitHub, sin convertir
necesariamente el proyecto en código abierto. Consulta la [documentación oficial](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/licensing-a-repository).

## Archivos de publicación

`.gitignore` conserva fuera de Git: binarios y ZIP, cachés, datos personales de
configuración/grabación, reportes brutos y documentación histórica/interna. Los
originales permanecen en disco. Solo se incluyen los recursos MiniTools usados.
No publiques un ZIP de toda la carpeta ni copies `.git`: contiene objetos locales
sin referencias que no forman parte del historial publicable.

Un archivo ignorado podría seguir rastreado o en commits previos. La revisión
inicial encontró cero archivos rastreados, cero commits alcanzables, sin ramas
confirmadas ni remoto. Se revisaron también 146 blobs locales sin coincidencias
en los patrones de credenciales configurados. No hubo reescritura ni eliminación
de objetos. Repite la revisión antes de cada publicación.

```powershell
./scripts/check-publication.ps1
./scripts/export-public.ps1
```

La exportación crea una carpeta/ZIP nuevo en `dist/` con el inventario no ignorado,
incluyendo los archivos que empiezan por punto y excluyendo `.git`. No exporta
binarios del programa ni macros del usuario. Es una alternativa para revisar los
archivos fuente sin compartir cachés e historial interno. El reporte automatizado
no sustituye una lectura humana del contenido y las capturas.

## Revisar y crear el primer commit

Estos comandos son para cuando se hayan decidido licencia y datos del repositorio.
La preparación local deja los archivos públicos en el índice, sin crear commits
ni cambiar tu identidad de Git. Si se agregan cambios nuevos, usa la lista explícita:

```powershell
git add -- .env.example .gitattributes .gitignore .github Directory.Build.props NuGet.Config global.json README.md SECURITY.md build.ps1 restore.ps1 test.ps1 scripts src tests assets/minitools-app.ico assets/minitools-mark.png docs/DESARROLLO.md docs/REVISION.md docs/PUBLICACION.md docs/screenshots docs/examples
./scripts/check-publication.ps1 -Staged
git diff --cached --check
git diff --cached --stat
git diff --cached
```

Revisa los nombres/correos que Git pondrá en el commit con `git var GIT_AUTHOR_IDENT`
y `git var GIT_COMMITTER_IDENT` **en tu terminal privada**. Si deseas mantener el
correo personal privado, configura localmente tu correo noreply real de GitHub
antes de crear el commit; no inventes ni publiques datos de otra persona.

```powershell
git commit -m 'chore: prepare MiniTools 1.4.2 for review'
git branch -M main
```

## Publicación externa: solo tras autorización explícita

Todavía no se ha creado ningún remoto ni se ha hecho push. Crea en GitHub un
repositorio vacío con el propietario, nombre y visibilidad que hayas elegido,
sin README/licencia automática que entren en conflicto con estos archivos.
Después de revisar el commit y confirmar la publicación, sustituye los valores
ficticios y ejecuta:

```powershell
$repoOwner = 'PROPIETARIO_ELEGIDO'
$repoName = 'NOMBRE_ELEGIDO'
git remote add origin "https://github.com/$repoOwner/$repoName.git"
git remote -v
git push -u origin main
```

Autentica Git con el gestor de credenciales de GitHub; no pongas un token en la URL,
script, .env ni historial de PowerShell. Si ya hubiera un remoto al repetir estos
pasos, verifica el destino antes de cambiarlo. Comprueba la primera ejecución de
CI, protección de rama y opciones de reporte privado/secret scanning que estén
disponibles. La CI aquí preparada no se ha ejecutado aún en GitHub.

Para releases futuras: después de CI y pruebas interactivas, adjunta únicamente
`dist/win-x64/MiniTools.exe`, versión, SHA256 y notas de prueba. Publicar la release
requiere también una instrucción explícita. Conserva las condiciones de licencia
acordadas para código y binarios.
