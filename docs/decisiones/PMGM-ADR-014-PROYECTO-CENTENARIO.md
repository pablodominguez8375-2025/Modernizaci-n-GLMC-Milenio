# PMGM-ADR-014 — Cambio de nombre institucional a Proyecto Centenario

## Estado
Aprobado por Product Owner.

## Decisión
El nombre institucional visible del producto cambia de **Proyecto Milenio** a **Proyecto Centenario**, en referencia a la celebración de los 100 años de la Gran Logia Mixta de Chile.

Nombre visible recomendado:

**Proyecto Centenario**  
Modernización Digital de la Gran Logia Mixta de Chile

Lema de proyecto:

**100 años de historia · Un legado hacia el futuro**

## Alcance del cambio
El nuevo nombre debe reflejarse progresivamente en:
- interfaz web y navegación;
- pantalla de acceso y encabezados;
- demo pública;
- documentación nueva;
- presentaciones ejecutivas;
- paquetes entregables futuros.

## Trazabilidad técnica
Para evitar romper referencias históricas y automatizaciones existentes:
- se conservan temporalmente identificadores técnicos `PMGM-*`;
- se mantiene el nombre actual del repositorio hasta una migración controlada;
- releases y documentos históricos conservan su denominación original cuando corresponda;
- el RC1 congelado no se modifica retroactivamente.

## Regla de producto
Este cambio es de identidad institucional y no altera la arquitectura funcional aprobada. Las imágenes y vistas de módulos ya aprobadas siguen siendo la base visual-arquitectónica del software; únicamente cambia la marca visible a **Proyecto Centenario**.