export interface AjaxOptions {
  url: string;
  method?: string;
  headers?: Record<string, string>;
  contentType?: string;
  dataType?: string;
  data?: string | Record<string, unknown>;
  traditional?: boolean;
  global?: boolean;
  [key: string]: unknown;
}

export interface XhrErrorObject {
  status: number;
  aborted: boolean;
  responseJSON: unknown;
  message?: string;
}

// Extends Promise with jQuery-compatible .done(), .fail(), .always(), .promise()
// so existing callers (still JS) don't need to change.
export interface AugmentedPromise<T> extends Promise<T> {
  done: (fn: (value: T) => void) => AugmentedPromise<T>;
  fail: (fn: (error: XhrErrorObject) => void) => AugmentedPromise<T>;
  always: (fn: (value?: unknown) => void) => AugmentedPromise<T>;
  promise: () => AugmentedPromise<T>;
}

export interface AjaxRequestResult<T = unknown> {
  request: AugmentedPromise<T>;
  abortRequest: () => void;
}

const absUrlRegex = /^(https?:)?\/\//i;
const apiRoot = window.Radarr.apiRoot;

function isRelative(ajaxOptions: AjaxOptions): boolean {
  return !absUrlRegex.test(ajaxOptions.url);
}

function addRootUrl(ajaxOptions: AjaxOptions): void {
  ajaxOptions.url = apiRoot + ajaxOptions.url;
}

function addApiKey(ajaxOptions: AjaxOptions): void {
  ajaxOptions.headers = ajaxOptions.headers || {};
  ajaxOptions.headers['X-Api-Key'] = window.Radarr.apiKey;
}

function addContentType(ajaxOptions: AjaxOptions): void {
  if (
    ajaxOptions.contentType == null &&
    ajaxOptions.dataType === 'json' &&
    (ajaxOptions.method === 'PUT' || ajaxOptions.method === 'POST' || ajaxOptions.method === 'DELETE')) {
    ajaxOptions.contentType = 'application/json';
  }
}

// Augment a native Promise with jQuery-compatible .done(), .fail(),
// .always() and .promise() methods so existing callers don't need to change.
function augmentPromise<T>(promise: Promise<T>): AugmentedPromise<T> {
  const augmented = promise as AugmentedPromise<T>;

  augmented.done = (fn) => {
    augmented.then(fn);
    return augmented;
  };

  augmented.fail = (fn) => {
    augmented.catch(fn);
    return augmented;
  };

  augmented.always = (fn) => {
    augmented.then(fn, fn);
    return augmented;
  };

  augmented.promise = () => augmented;

  // Wrap .then/.catch to propagate the augmented methods
  const originalThen = augmented.then.bind(augmented);

  augmented.then = ((...args: Parameters<typeof originalThen>) =>
    augmentPromise(originalThen(...args))) as typeof augmented.then;

  const originalCatch = augmented.catch.bind(augmented);

  augmented.catch = ((...args: Parameters<typeof originalCatch>) =>
    augmentPromise(originalCatch(...args))) as typeof augmented.catch;

  return augmented;
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export default function createAjaxRequest<T = any>(originalAjaxOptions: AjaxOptions): AjaxRequestResult<T> {
  const controller = new AbortController();
  let aborted = false;
  let complete = false;

  function abortRequest(): void {
    if (!complete) {
      aborted = true;
      controller.abort();
    }
  }

  const ajaxOptions: AjaxOptions = { ...originalAjaxOptions };

  if (isRelative(ajaxOptions)) {
    addRootUrl(ajaxOptions);
    addApiKey(ajaxOptions);
    addContentType(ajaxOptions);
  }

  const fetchOptions: RequestInit = {
    method: ajaxOptions.method || 'GET',
    headers: { ...ajaxOptions.headers },
    signal: controller.signal
  };

  if (ajaxOptions.contentType) {
    (fetchOptions.headers as Record<string, string>)['Content-Type'] = ajaxOptions.contentType;
  }

  if (ajaxOptions.data) {
    fetchOptions.body = typeof ajaxOptions.data === 'string' ? ajaxOptions.data : JSON.stringify(ajaxOptions.data);
  }

  const fetchPromise = fetch(ajaxOptions.url, fetchOptions)
    .then(async (response): Promise<T> => {
      complete = true;

      if (!response.ok) {
        // Build an error object that mimics jQuery's XHR shape for callers
        const errorXhr: XhrErrorObject = {
          status: response.status,
          aborted,
          responseJSON: null
        };

        try {
          errorXhr.responseJSON = await response.json();
        } catch {
          // response wasn't JSON
        }

        throw errorXhr;
      }

      const text = await response.text();

      if (!text) {
        return {} as T;
      }

      try {
        return JSON.parse(text) as T;
      } catch {
        return text as unknown as T;
      }
    })
    .catch((error): never => {
      complete = true;

      if (error && error.name === 'AbortError') {
        const abortXhr: XhrErrorObject = { status: 0, aborted: true, responseJSON: null };
        throw abortXhr;
      }

      // If it's already our shaped error object, re-throw as-is
      if (error && typeof error.status === 'number') {
        throw error;
      }

      // Network error or other unexpected failure
      const networkXhr: XhrErrorObject = { status: 0, aborted, responseJSON: null };
      throw networkXhr;
    });

  const request = augmentPromise<T>(fetchPromise);

  return {
    request,
    abortRequest
  };
}
