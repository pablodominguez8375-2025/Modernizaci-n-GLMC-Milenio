# PMGM-DEV-002 — Plan de sprint P0: flujos institucionales 2026

**Rama:** `dev`  
**Objetivo:** cerrar brechas P0 de PMGM-GAP-001 sin romper QA existente.

## Orden de trabajo

1. **Extender catálogo de ceremonias**
   - agregar afiliación e incorporación;
   - mantener iniciación, aumento de salario y exaltación;
   - preparar tipo extensible para “otra” sin autorizar automáticamente.

2. **Matriz de validaciones**
   - Régimen Interior;
   - Gran Tesorería;
   - Gran Hospitalaria;
   - Gran Maestría;
   - publicación del insinuado para iniciación;
   - requisitos específicos por tipo de ceremonia.

3. **Expediente de insinuación**
   - presentación 1.er grado;
   - espera de 7 días;
   - deliberación y unanimidad;
   - publicación;
   - 20 días corridos;
   - tres entrevistas;
   - antecedentes complementarios;
   - votación de 3.er grado;
   - balotaje de 1.er grado;
   - rechazo y nueva presentación.

4. **Evidencias financieras desagregadas**
   - cuota mensual;
   - derecho de ceremonia;
   - Fondo de Defunción cuando aplique;
   - reposiciones cuando aplique;
   - Hospitalidad cuando aplique;
   - conciliación y estado agregado derivado.

5. **Autorización y plancha**
   - impedir autorización si falta un requisito obligatorio;
   - generar plancha desde datos ya validados;
   - numeración auditable;
   - cierre posterior de ceremonia e historial.

6. **Pruebas**
   - pruebas unitarias de políticas;
   - pruebas API de transiciones válidas e inválidas;
   - ejecutar PMGM-QA-003 en ambiente QA.

## Regla técnica
Los plazos, estados y requisitos habilitantes se validan en backend. La interfaz sólo representa el estado; no constituye la fuente de verdad.

## Definition of Done
- migraciones aplicables ejecutan correctamente;
- API compila y pruebas pasan;
- frontend muestra matriz de requisitos;
- auditoría registra transiciones;
- UAT crítico aprobado;
- documentación actualizada;
- no se promueve a `main` sin aprobación del Product Owner.
