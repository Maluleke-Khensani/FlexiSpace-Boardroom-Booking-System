export const msalConfig = {
    auth: {
        clientId: "77163347-59be-48f4-8675-535af30a3a53",
        authority: "https://login.microsoftonline.com/2427e95a-1238-4880-8236-996db36bc62c",
        redirectUri: "http://localhost:5173"
    }
};

export const loginRequest = {
    scopes: ["api://77163347-59be-48f4-8675-535af30a3a53/access_as_user"]
};