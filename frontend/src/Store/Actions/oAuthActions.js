import { createAction } from 'redux-actions';
import { batchActions } from 'redux-batched-actions';
import { set } from 'Store/Actions/baseActions';
import { createThunk, handleThunks } from 'Store/thunks';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import requestAction from 'Utilities/requestAction';
import getSectionState from 'Utilities/State/getSectionState';
import updateSectionState from 'Utilities/State/updateSectionState';
import translate from 'Utilities/String/translate';
import createHandleActions from './Creators/createHandleActions';

//
// Variables

export const section = 'oAuth';
const callbackUrl = `${window.location.origin}${window.Radarr.urlBase}/oauth.html`;

//
// State

export const defaultState = {
  authorizing: false,
  result: null,
  error: null
};

//
// Actions Types

export const START_OAUTH = 'oAuth/startOAuth';
export const SET_OAUTH_VALUE = 'oAuth/setOAuthValue';
export const RESET_OAUTH = 'oAuth/resetOAuth';

//
// Action Creators

export const startOAuth = createThunk(START_OAUTH);
export const setOAuthValue = createAction(SET_OAUTH_VALUE);
export const resetOAuth = createAction(RESET_OAUTH);

//
// Helpers

// Per-flow OAuth callback map keyed by nonce.
// Each popup gets a unique window.name which oauth.html uses to look up the right callback.
if (!window._oauthCallbacks) {
  window._oauthCallbacks = {};
}

function showOAuthWindow(url, payload) {
  const nonce = `oauth_${Math.random().toString(36).slice(2)}`;
  const newWindow = window.open(url, nonce);

  if (
    !newWindow ||
     newWindow.closed ||
     typeof newWindow.closed == 'undefined'
  ) {

    // A fake validation error to mimic a 400 response from the API.
    const error = {
      status: 400,
      responseJSON: [
        {
          propertyName: payload.name,
          errorMessage: translate('OAuthPopupMessage')
        }
      ]
    };

    return Promise.reject(error);
  }

  return new Promise((resolve, reject) => {
    const pollInterval = setInterval(() => {
      if (newWindow.closed) {
        clearInterval(pollInterval);
        delete window._oauthCallbacks[nonce];

        const error = {
          status: 400,
          responseJSON: [
            {
              propertyName: payload.name,
              errorMessage: translate('OAuthPopupMessage')
            }
          ]
        };

        reject(error);
      }
    }, 500);

    window._oauthCallbacks[nonce] = function(query, onComplete) {
      clearInterval(pollInterval);
      delete window._oauthCallbacks[nonce];

      const queryParams = {};
      const splitQuery = query.substring(1).split('&');

      splitQuery.forEach((param) => {
        if (param) {
          const paramSplit = param.split('=');

          queryParams[decodeURIComponent(paramSplit[0])] = decodeURIComponent(paramSplit[1]);
        }
      });

      onComplete();
      resolve(queryParams);
    };
  });
}

function executeIntermediateRequest(payload, ajaxOptions) {
  return createAjaxRequest(ajaxOptions).request.then((data) => {
    return requestAction({
      action: 'continueOAuth',
      queryParams: {
        ...data,
        callbackUrl
      },
      ...payload
    });
  });
}

//
// Action Handlers

export const actionHandlers = handleThunks({

  [START_OAUTH]: function(getState, payload, dispatch) {
    const {
      name,
      section: actionSection,
      ...otherPayload
    } = payload;

    const actionPayload = {
      action: 'startOAuth',
      queryParams: { callbackUrl },
      ...otherPayload
    };

    dispatch(setOAuthValue({
      authorizing: true
    }));

    let startResponse = {};

    const promise = requestAction(actionPayload)
      .then((response) => {
        startResponse = response;

        if (response.oauthUrl) {
          return showOAuthWindow(response.oauthUrl, payload);
        }

        return executeIntermediateRequest(otherPayload, response).then((intermediateResponse) => {
          startResponse = intermediateResponse;

          return showOAuthWindow(intermediateResponse.oauthUrl, payload);
        });
      })
      .then((queryParams) => {
        return requestAction({
          action: 'getOAuthToken',
          queryParams: {
            ...startResponse,
            ...queryParams
          },
          ...otherPayload
        });
      })
      .then((response) => {
        dispatch(setOAuthValue({
          authorizing: false,
          result: response,
          error: null
        }));
      });

    promise.done(() => {
      // Clear any previously set save error.
      dispatch(set({
        section: actionSection,
        saveError: null
      }));
    });

    promise.fail((xhr) => {
      const actions = [
        setOAuthValue({
          authorizing: false,
          result: null,
          error: xhr
        })
      ];

      if (xhr.status === 400) {
        // Set a save error so the UI can display validation errors to the user.
        actions.splice(0, 0, set({
          section: actionSection,
          saveError: xhr
        }));
      }

      dispatch(batchActions(actions));
    });
  }

});

//
// Reducers

export const reducers = createHandleActions({

  [SET_OAUTH_VALUE]: function(state, { payload }) {
    const newState = Object.assign(getSectionState(state, section), payload);

    return updateSectionState(state, section, newState);
  },

  [RESET_OAUTH]: function(state) {
    return updateSectionState(state, section, defaultState);
  }

}, defaultState, section);
