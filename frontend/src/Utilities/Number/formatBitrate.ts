const UNITS = ['bps', 'kbps', 'Mbps', 'Gbps', 'Tbps'];

function formatBitrate(input: string | number) {
  const size = Number(input);

  if (isNaN(size)) {
    return '';
  }

  if (size === 0) {
    return '0 bps/s';
  }

  // Input is in bits per second; use base-10 divisions (1000)
  const exponent = Math.min(
    Math.floor(Math.log(Math.abs(size)) / Math.log(1000)),
    UNITS.length - 1
  );

  const value = size / Math.pow(1000, exponent);

  return `${value.toFixed(1)} ${UNITS[exponent]}/s`;
}

export default formatBitrate;
