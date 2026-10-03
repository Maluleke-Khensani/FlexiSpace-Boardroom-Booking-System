export const msalConfig = {
    auth: {
        clientId: "77163347-59be-48f4-8675-535af30a3a53",
        authority: "https://login.microsoftonline.com/common", 
        redirectUri: "http://localhost:5173"
    }
};

export const loginRequest = {
    scopes: ["api://77163347-59be-48f4-8675-535af30a3a53/access_as_user"]
};

export const apiRequest = {
    scopes: ["api://77163347-59be-48f4-8675-535af30a3a53/access_as_user"],
    // Use whichever port your API actually runs on (check launchSettings.json)
};

