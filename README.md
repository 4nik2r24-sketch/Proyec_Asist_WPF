# Sistema de Control de Asistencia (MVP)

Aplicación de escritorio desarrollada en WPF y C# para la gestión del control de asistencia de empleados. Permite el registro de marcas de tiempo y la administración integral del personal de una empresa.

## Características Principales
* **Autenticación Segura:** Inicio de sesión con diferenciación de roles (Administrador y Empleado).
* **Panel de Empleado:** Registro de entrada y salida con reloj en tiempo real e historial personal.
* **Gestión de Usuarios:** Módulo de administración (CRUD) para crear, modificar y eliminar perfiles de trabajadores.
* **Reportes Automáticos:** Sistema de filtros para detectar atrasos, salidas anticipadas e inasistencias.
* **Exportación de Datos:** Descarga directa de los reportes administrativos en formato CSV (Excel).

## Tecnologías Utilizadas
| Tecnología | Rol en el Proyecto |
| :--- | :--- |
| **C# / WPF** | Lenguaje principal y diseño de la interfaz gráfica de usuario. |
| **.NET 8.0** | Entorno de ejecución y marco de trabajo principal. |
| **MySQL** | Base de datos relacional para el almacenamiento de credenciales y registros. |

## Librerías Descargadas (NuGet)
* **MaterialDesignThemes**: Proporciona la paleta de colores y componentes visuales modernos (estilo Material Design) para la interfaz de usuario.
* **MySql.Data**: Permite la conexión fluida y ejecución de consultas SQL entre la aplicación .NET y la base de datos MySQL.

## Credenciales de Prueba
Para el modo de pruebas o "Modo Admin", utiliza la siguiente clave de seguridad interna (Centro de Control):
* **Clave Administrativa (Centro de Control):** `admin123`

## Instrucciones: Descarga y Ejecución Paso a Paso

Sigue estos pasos para instalar y hacer correr la aplicación en tu entorno local:

### 1. Clonar el Repositorio
Abre tu terminal (PowerShell o Git Bash) y ejecuta el siguiente comando para descargar el código fuente a tu computadora:
`git clone https://github.com/4nik2r24-sketch/Proyec_Asist_WPF.git`

### 2. Configurar la Base de Datos (MySQL)
1. Inicia tu servidor local de MySQL (por ejemplo, a través de XAMPP, WAMP o MySQL Workbench).
2. Crea una nueva base de datos llamada `sistema_asistencia`.
3. Ejecuta el script SQL incluido en el proyecto (si está disponible) para crear las tablas necesarias (`usuario`, `rol`, `asistencia`). Si no tienes el script, deberás construir las tablas basándote en los modelos del código.

### 3. Actualizar la Cadena de Conexión
1. Abre la solución del proyecto (`.sln`) usando **Visual Studio**.
2. En el *Explorador de soluciones*, busca y abre el archivo `ConexionBD.cs`.
3. Modifica la variable `cadenaConexion`. Reemplaza el texto `tu_contrasena` por la contraseña real del usuario `root` de tu servidor MySQL local:
   `Server=localhost; Database=sistema_asistencia; Uid=root; Pwd=tu_contrasena;`
4. Guarda los cambios.

### 4. Limpiar y Recompilar
Antes de ejecutar la aplicación, asegúrate de que no haya errores de dependencias:
1. En el menú superior de Visual Studio, ve a **Generar** -> **Limpiar solución**.
2. Luego ve a **Generar** -> **Recompilar solución**.

### 5. Ejecutar la Aplicación
* Presiona la tecla **F5** o haz clic en el botón de **Iniciar** en Visual Studio para arrancar el entorno de desarrollo y probar el sistema de inicio de sesión.

### 6. Publicación (Crear un archivo .exe para compartir)
Si deseas generar la versión final lista para compartir:
1. En el *Explorador de soluciones*, haz clic derecho en el proyecto principal y selecciona **Publicar...**.
2. Selecciona **Carpeta** como destino, avanza y haz clic en **Finalizar** y luego en **Cerrar**.
3. En la pantalla de resumen, en la sección de "Hospedaje" o "Ubicación de destino", haz clic en el ícono del **lápiz** (o *Mostrar todas las configuraciones*).
4. Configura exactamente así:
   * **Configuración:** `Release | Any CPU`
   * **Modo de implementación:** `Autocontenido`
   * **Tiempo de ejecución de destino:** `win-x64`
   * **Opciones de publicación de archivos:** Marca la casilla `Producir un único archivo`.
5. Guarda y haz clic en **Publicar**.
6. Navega a la ruta `bin\Release\net8.0-windows\win-x64\publish`. Allí encontrarás un único y pesado archivo `.exe` que podrás compartir para que otros usuarios lo ejecuten sin instalar dependencias.
