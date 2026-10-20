# QA — tareas de inicialización completadas (#525)

Evidencia física20oct01:35Z: firma, manifiesto, respaldo, salud y build web pasaron. El preflight bloqueó antes de apply por DEPENDENCY_NOT_RUNNING. Servicios permanentes están running; db-grants, migrator y minio-init están exited con código0 legítimo. La web anterior no se reemplazó.

Corrección reclamada: aceptar únicamente esos tres jobs exited0, comprobar su código real en Docker, conservarlos en baseline para detectar cualquier cambio posterior. Seguir rechazando servicios caídos, jobs fallidos/desconocidos y tareas aún activas. Agregar regresiones y jobs reales completados al ensayo Docker. Repinar bootstrap sin alterar código ya instalado ni contenedores en glmdch.

Pendiente CI exact-head, integración y bootstrap corregido por Termius. No declarar desplegado, timer activo, G10 o UAT aprobado. Main/srv01/BD/Silo/red intactos. Renovación del respaldo externo sigue pendiente.
