# #525 — apertura inicial de QA

El Sponsor reportó lentitud antes del ingreso. Seis pruebas de salud desdeglmdch31–143ms HTTP200 yRAMdisponible4.5GiB no muestran saturación; unoutlier5s ocurrió antes deHTTP. Navegadorpúblico pudoabrirportada/formularioOIDC; sincredenciales.

Cambio: AuthRoot difiere App.tsx medianteReactlazy/Suspense hasta renderautenticado/demo. Fallbackcorporativo yrecargasegura si unchunk antiguo desaparece. Autenticación/permisos/clientes/datos sin cambios. app-styles.ts precargaCSScomponentes en elordenoriginal para conservarcascada global; NO se editaron CSS ni App.tsx/temas/workflows.

Buildantes/después mismoentorno:JSinicial1366006bytes/gzip338591 →599019aprox/gzip156k,54%menos. Mediciónincluyegrafo estático completo. CSSfinal273595bytes EXACTAMENTEIGUALaldeplegado,index-gFDC5GXH.css,SHA2569c07c66914f63c6006f43b71474d4f4debfc7242f3660aadd110a3269e7e06cf. CSSsigueglobalpara evitaralterarcascada; no prometerreducciónCSS. No sepromete tiempoAndroidexacto sinmediciónfinal.

Nginx:assets conhash publicmax-age1añoimmutable; faltantes404sinSPAfallback; HTMLno-cache; headersseguridadpreservadosycallback/logoutno-store. No cacheAPI,credenciales,tokens ni qa-version.

23testslocalesPASS(acceso,sesión,config,presupuesto/grafo ycache/seguridad),buildtipadoPASS,lintPASS conavisoexistenteno-control-regex. Regresión construyebundleproduction real:Appfuera delgrafoinicial,JSgzip<180k,todoCSSenunarchivoinicial. CI debecomprobarvisual360/390/768/1440; no afirmar capturaslocalesque no sehicieron. DespliegueporagenteQAexistente tras5gates+publicaciónfirmada; la versiónfísicaantesdelcambioes4c0101fa comprobada porqa-version público. No volvera pedirbootstrap.

PendienteCIexactHEAD,integraciónGOV003§7,publicaciónyverificaciónfísica delnuevoSHAyheaders. Reservado#533comentario6290212642. Respaldoexternoválidohasta21oct14:52Chile; renovaciónaúnpendiente. Main/srv01/backend/DB/Silo intactos. ElPR587contieneSHA/estadovigente.
