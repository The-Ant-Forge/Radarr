const UNITS = ['B', 'KiB', 'MiB', 'GiB', 'TiB', 'PiB'];

function formatBytes(input: string | number) {
  const size = Number(input);

  if (isNaN(size)) {
    return '';
  }

  if (size === 0) {
    return '0 B';
  }

  const exponent = Math.min(
    Math.floor(Math.log(Math.abs(size)) / Math.log(1024)),
    UNITS.length - 1
  );

  const value = size / Math.pow(1024, exponent);

  return `${value.toFixed(1)} ${UNITS[exponent]}`;
}

export default formatBytes;
