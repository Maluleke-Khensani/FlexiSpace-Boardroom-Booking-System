import { useMsal } from '@azure/msal-react'
import { loginRequest, apiRequest } from './authConfig'
import { useState } from 'react'

function App() {
    const { instance, accounts } = useMsal()
    const [apiResult, setApiResult] = useState(null)
    const [apiError, setApiError] = useState(null)

    const callApi = async () => {
        setApiError(null)
        setApiResult(null)
        try {
            const tokenResponse = await instance.acquireTokenSilent({
                ...apiRequest,
                account: accounts[0]
            })

const response = await fetch(
    'https://localhost:7055/api/GraphTest/create-test-event',
    {
        method: 'POST',
        headers: {
            Authorization: `Bearer ${tokenResponse.accessToken}`
        }
    }
)

            if (!response.ok) {
                setApiError(`Request failed: ${response.status}`)
                return
            }

            const data = await response.json()
            setApiResult(data)
        } catch (err) {
            setApiError(err.message)
        }
    }

    const handleLogin = () => {
        instance.loginRedirect(loginRequest)
    }

    const handleLogout = () => {
        instance.logoutRedirect()
    }

    const isLoggedIn = accounts.length > 0
    const account = accounts[0]

    return (
        <div>
            <h1>FlexiSpace Authentication Test</h1>

            {isLoggedIn ? (
                <div>
                    <h2>✅ Signed in successfully!</h2>

                    <p>
                        <strong>Name:</strong> {account.name}
                    </p>

                    <p>
                        <strong>Email:</strong> {account.username}
                    </p>

                    <button onClick={handleLogout}>
                        Sign out
                    </button>

                    <button onClick={callApi} style={{ marginLeft: '10px' }}>
                        Call protected API
                    </button>

                    {apiResult && (
                        <pre>{JSON.stringify(apiResult, null, 2)}</pre>
                    )}

                    {apiError && (
                        <p style={{ color: 'red' }}>{apiError}</p>
                    )}
                </div>
            ) : (
                <div>
                    <p>You are not currently signed in.</p>

                    <button onClick={handleLogin}>
                        Sign in with Microsoft
                    </button>
                </div>
            )}
        </div>
    )
}

export default App