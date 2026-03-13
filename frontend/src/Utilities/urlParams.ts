// Replacement for jQuery's $.param() — serializes an object to a URL query string.
// Handles nested values similarly to $.param(obj, traditional=true).
export default function urlParams(
  obj:
    | Record<string, string | number | boolean | string[] | null | undefined>
    | null
    | undefined
): string {
  if (!obj) {
    return '';
  }

  const params = new URLSearchParams();

  Object.keys(obj).forEach((key) => {
    const value = obj[key];

    if (value == null) {
      return;
    }

    if (Array.isArray(value)) {
      value.forEach((v) => params.append(key, v));
    } else {
      params.append(key, String(value));
    }
  });

  return params.toString();
}
