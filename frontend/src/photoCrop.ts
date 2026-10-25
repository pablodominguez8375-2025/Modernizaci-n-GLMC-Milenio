/** Recorte cuadrado centrado y reducción de la foto de perfil (#609). El servidor vuelve a validar todo. */
export const PHOTO_OUTPUT_SIDE = 512
export const PHOTO_MAX_BYTES = 400 * 1024
export const PHOTO_MAX_INPUT_BYTES = 12 * 1024 * 1024

export function squareCropRect(width: number, height: number): { sx: number; sy: number; side: number } {
  const side = Math.max(1, Math.min(width, height))
  return { sx: Math.floor((width - side) / 2), sy: Math.floor((height - side) / 2), side }
}

export function outputSide(side: number): number {
  return Math.min(side, PHOTO_OUTPUT_SIDE)
}

/** Devuelve un JPG cuadrado sin metadatos (el lienzo no conserva EXIF) que cabe en el límite del servidor. */
export async function cropToSquareJpeg(file: File): Promise<Blob> {
  if (!/^image\/(jpeg|png)$/.test(file.type)) throw new Error('La foto debe ser JPG o PNG.')
  if (file.size > PHOTO_MAX_INPUT_BYTES) throw new Error('La imagen es demasiado grande. Elija una de hasta 12 MB.')
  const bitmap = await createImageBitmap(file)
  const { sx, sy, side } = squareCropRect(bitmap.width, bitmap.height)
  if (side < 200) throw new Error('La foto es muy pequeña. Use una imagen de al menos 200 × 200 píxeles.')
  const target = outputSide(side)
  const canvas = document.createElement('canvas')
  canvas.width = target; canvas.height = target
  const context = canvas.getContext('2d')
  if (!context) throw new Error('Este navegador no puede preparar la foto.')
  context.drawImage(bitmap, sx, sy, side, side, 0, 0, target, target)
  bitmap.close?.()
  for (const quality of [0.88, 0.8, 0.7, 0.6]) {
    const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', quality))
    if (blob && blob.size <= PHOTO_MAX_BYTES) return blob
  }
  throw new Error('No fue posible reducir la foto al tamaño permitido. Pruebe con otra imagen.')
}
