
import { useState, type FormEvent } from 'react'
import './App.css'

type LoginResponse = {
    accessToken: string
    tokenType: string
    expiresAtUtc: string
    fullName: string
    roles: string[]
}

type CurrentUser = {
    id: string
    email: string
    fullName: string
    roles: string[]
}

type Session = {
    token: string
    user: CurrentUser
}

function App() {
    const [email, setEmail] = useState('')
    const [password, setPassword] = useState('')
    const [session, setSession] = useState<Session | null>(null)
    const [isLoading, setIsLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)

    async function handleLogin(event: FormEvent<HTMLFormElement>) {
        event.preventDefault()
        setError(null)
        setIsLoading(true)

        try {
            // Step 1: Authenticate against ASP.NET Core.

            const loginResponse = await fetch('/api/auth/login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    email: email.trim(),
                    password,
                }),
            })

            if (loginResponse.status === 401) {
                throw new Error('Nieprawidłowy e-mail lub hasło.')
            }

            if (!loginResponse.ok) {
                throw new Error(
                    `Nie udało się zalogować (HTTP ${loginResponse.status}).`
                )
            }

            const loginData: LoginResponse = await loginResponse.json()

            if (!loginData.accessToken) {
                throw new Error('Serwer nie zwrócił tokenu JWT.')
            }

            // Step 2: Verify token using protected API endpoint.

            const meResponse = await fetch('/api/auth/me', {
                headers: {
                    Authorization: `Bearer ${loginData.accessToken}`,
                },
            })

            if (!meResponse.ok) {
                throw new Error(
                    `Nie udało się pobrać użytkownika (HTTP ${meResponse.status}).`
                )
            }

            const user: CurrentUser = await meResponse.json()

            // Keep JWT only in memory for this first version.
            setSession({
                token: loginData.accessToken,
                user,
            })

            setPassword('')
        } catch (err) {
            setError(
                err instanceof Error
                    ? err.message
                    : 'Wystąpił nieznany błąd podczas logowania.'
            )
        } finally {
            setIsLoading(false)
        }
    }

    function handleLogout() {
        setSession(null)
        setPassword('')
        setError(null)
    }

    if (session) {
        return (
            <div className="app-page">
                <header className="app-header">
                    <div className="brand">
                        <div className="brand-icon">W</div>
                        <span>Warehouse Management System</span>
                    </div>

                    <button
                        className="logout-button"
                        onClick={handleLogout}
                    >
                        Wyloguj
                    </button>
                </header>

                <main className="dashboard">
                    <div className="status-label">
                        <span className="status-dot" />
                        Zalogowano pomyślnie
                    </div>

                    <h1>Witaj, {session.user.fullName}!</h1>

                    <p className="subtitle">
                        Połączenie React z ASP.NET Core API działa prawidłowo.
                    </p>

                    <section className="info-card">
                        <h2>Informacje o użytkowniku</h2>

                        <div className="info-row">
                            <span>Imię i nazwisko</span>
                            <strong>{session.user.fullName}</strong>
                        </div>

                        <div className="info-row">
                            <span>E-mail</span>
                            <strong>{session.user.email}</strong>
                        </div>

                        <div className="info-row">
                            <span>Role</span>
                            <strong>
                                {session.user.roles.join(', ') || 'Brak ról'}
                            </strong>
                        </div>

                        <div className="info-row">
                            <span>JWT</span>
                            <strong>Token otrzymany i zweryfikowany</strong>
                        </div>
                    </section>

                    <p className="next-step">
                        Następny etap: panel magazynowy i nawigacja.
                    </p>
                </main>
            </div>
        )
    }

    return (
        <div className="login-page">
            <main className="login-card">
                <div className="login-brand">
                    <div className="brand-icon brand-icon-large">W</div>
                    <span>WAREHOUSE SYSTEM</span>
                </div>

                <h1>Logowanie</h1>

                <p className="subtitle">
                    Zaloguj się do systemu zarządzania magazynem.
                </p>

                <form onSubmit={handleLogin}>
                    <div className="field">
                        <label htmlFor="email">Adres e-mail</label>
                        <input
                            id="email"
                            type="email"
                            autoComplete="username"
                            placeholder="E-mail użytkownika"
                            value={email}
                            onChange={(event) => setEmail(event.target.value)}
                            required
                            disabled={isLoading}
                        />
                    </div>

                    <div className="field">
                        <label htmlFor="password">Hasło</label>
                        <input
                            id="password"
                            type="password"
                            autoComplete="current-password"
                            placeholder="Wprowadź hasło"
                            value={password}
                            onChange={(event) => setPassword(event.target.value)}
                            required
                            disabled={isLoading}
                        />
                    </div>

                    {error && (
                        <div className="error-message" role="alert">
                            {error}
                        </div>
                    )}

                    <button
                        type="submit"
                        className="login-button"
                        disabled={isLoading}
                    >
                        {isLoading ? 'Logowanie...' : 'Zaloguj się'}
                    </button>
                </form>

                <p className="login-footer">
                    Warehouse Management System
                    <br />
                    Projekt inżynierski
                </p>
            </main>
        </div>
    )
}

export default App
