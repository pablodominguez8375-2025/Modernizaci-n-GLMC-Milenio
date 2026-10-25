# #533 C14 acreditación de períodos de Venerable Maestro

Fecha 2026-10-19, base dev@b77d460fa97f476fa01d44584f71261da795fc54; main@6dfb9546a4873baff15955cf86abfd7d47e3d111 protegido.

Propósito: evitar declarar a un hermano asambleísta permanente solamente por `OfficeAssignment.EndDate`. Nuevo adaptador puro combina cargos reales del dominio Membership con **atestaciones oficiales de cada período**, comprobando MemberId/Taller/ID, fechas y referencias; la categoría se obtiene con `AssemblyAccreditedCategoryPolicy` existente y nunca se construye por dato del navegador.

Sin catálogo institucional canónico de `OfficeType` ni cobertura certificada completa de los períodos a corte: estado **pendiente**, ningún asambleísta acreditado. El registro verificado debe venir de servicios backend independientes, no booleanos enviados por HTTP. En renovaciones, un período íntegro mantiene categoría permanente; primer mandato actual verificado da representante. El mapa V.M. no acredita per se asistencia/sufragio (siguen GT/RI/Tribunal/C12).

No endpoint, no modificación de BD ni datos reales. Pruebas unitarias de períodos certificados, falta de evidencia, ámbito cruzado, fecha inválida y reelección. PR draft hasta 3/3 CI/Showcase/Installable exact-head. QA físico y main aparte. C12 y C13 en PR independientes.
