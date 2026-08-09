import { useMsal } from '@azure/msal-react'
import { loginRequest } from './authConfig'

function App() {
    const { instance, accounts } = useMsal()

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