import { clsx } from 'clsx';

/**
 * Material Symbols Outlined icon wrapper.
 * @param {{ name: string, filled?: boolean, className?: string, size?: number }} props
 */
export default function MaterialIcon({ name, filled = false, className, size }) {
  return (
    <span
      className={clsx('material-symbols-outlined select-none', className)}
      style={{
        fontVariationSettings: filled ? "'FILL' 1" : undefined,
        fontSize: size ? `${size}px` : undefined,
      }}
    >
      {name}
    </span>
  );
}
