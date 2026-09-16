import { useMsal } from '@azure/msal-react'
import { loginRequest, apiRequest } from './authConfig'
import { useState } from 'react'
import { AddUserModal } from './AddUserModal' // Make sure the path matches your project structure

function App() {
    const { instance, accounts } = useMsal()
    const [apiResult, setApiResult] = useState(null)
    const [apiError, setApiError] = useState(null)

    // State for managing the modal
    const [isModalOpen, setIsModalOpen] = useState(false)
    const [accessToken, setAccessToken] = useState('')

    // Sample locations matching your FlexiSpace application data
    const locations = [
        { id: 1, name: 'Centurion' },
        { id: 2, name: 'Houghton' },
        { id: 3, name: 'Eagle Canyon' }
    ]

    // Fetch token on demand when opening the modal
    const handleOpenModal = async () => {
        try {
            const tokenResponse = await instance.acquireTokenSilent({
                ...apiRequest,
                account: accounts[0]
            })
            setAccessToken(tokenResponse.accessToken)
            setIsModalOpen(true)
        } catch (err) {
            setApiError(`Failed to acquire token for modal: ${err.message}`)
        }
    }

    const callApi = async () => {
        setApiError(null)
        setApiResult(null)
        try {
            const tokenResponse = await instance.acquireTokenSilent({
                ...apiRequest,
                account: accounts[0]
            })

<<<<<<< Updated upstream:FlexiSpace/FlexiSpace.TestClient/src/App.jsx
const response = await fetch('https://localhost:7055/api/location', {                
    headers: {
                    Authorization: `Bearer ${tokenResponse.accessToken}`
                }
            })
=======
            console.log("ACCESS TOKEN:", tokenResponse.accessToken)

            const response = await fetch(
                'https://localhost:7055/api/GraphTest/create-test-event',
                {
                    method: 'POST',
                    headers: {
                        Authorization: `Bearer ${tokenResponse.accessToken}`
                    }
                }
            )
>>>>>>> Stashed changes:FlexiSpace.TestClient/src/App.jsx

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

    const handleUserCreated = (newUser) => {
        console.log('Successfully provisioned FlexiSpace user:', newUser)
        setApiResult({ message: 'User provisioned successfully!', user: newUser })
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
        <div style={{ padding: '20px' }}>
            <h1>FlexiSpace Administration</h1>

            {isLoggedIn ? (
                <div>
                    <h2>✅ Signed in successfully!</h2>

                    <p>
                        <strong>Name:</strong> {account.name}
                    </p>

                    <p>
                        <strong>Email:</strong> {account.username}
                    </p>

                    <div style={{ display: 'flex', gap: '10px', marginTop: '15px' }}>
                        <button onClick={handleLogout}>
                            Sign out
                        </button>

                        <button onClick={callApi}>
                            Call protected API
                        </button>

                        <button 
                            onClick={handleOpenModal}
                            style={{ backgroundColor: '#2563eb', color: '#fff', border: 'none', padding: '8px 16px', borderRadius: '4px', cursor: 'pointer' }}
                        >
                            + Provision User
                        </button>
                    </div>

                    {apiResult && (
                        <div style={{ marginTop: '20px' }}>
                            <h3>Result Output:</h3>
                            <pre style={{ background: '#f4f4f5', padding: '10px', borderRadius: '4px' }}>
                                {JSON.stringify(apiResult, null, 2)}
                            </pre>
                        </div>
                    )}

                    {apiError && (
                        <p style={{ color: 'red', marginTop: '10px' }}>{apiError}</p>
                    )}

                    {/* Render Child Component */}
                    <AddUserModal
                        isOpen={isModalOpen}
                        onClose={() => setIsModalOpen(false)}
                        accessToken={accessToken}
                        locations={locations}
                        onUserCreated={handleUserCreated}
                    />
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