
import { useCallback, useState, type FormEvent } from 'react'
import ProductsPage from './pages/ProductsPage'
import WarehousesPage from './pages/WarehousesPage.tsx'
import PalletsPage from './pages/PalletsPage'
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

type PageId =
  | 'dashboard'
  | 'products'
  | 'warehouses'
  | 'pallets'
  | 'receipts'
  | 'orders'
  | 'shipments'
  | 'users'

type MenuItem = {
  id: PageId
  label: string
  icon: string
  adminOnly?: boolean
}

type MenuGroup = {
  title: string
  items: MenuItem[]
}

const navigation: MenuGroup[] = [
  {
    title: 'GŁÓWNE',
    items: [
      { id: 'dashboard', label: 'Pulpit', icon: '▦' },
    ],
  },
  {
    title: 'MAGAZYN',
    items: [
      { id: 'products', label: 'Produkty', icon: '◈' },
      {
        id: 'warehouses',
        label: 'Magazyny i lokalizacje',
        icon: '⌂',
      },
      { id: 'pallets', label: 'Palety LE', icon: '▣' },
    ],
  },
  {
    title: 'OPERACJE',
    items: [
      {
        id: 'receipts',
        label: 'Przyjęcia towaru',
        icon: '↙',
      },
      {
        id: 'orders',
        label: 'Zamówienia i kompletacja',
        icon: '☷',
      },
      {
        id: 'shipments',
        label: 'Wysyłki',
        icon: '↗',
      },
    ],
  },
  {
    title: 'SYSTEM',
    items: [
      {
        id: 'users',
        label: 'Użytkownicy',
        icon: '♙',
        adminOnly: true,
      },
    ],
  },
]

const pageTitles: Record<PageId, string> = {
  dashboard: 'Pulpit',
  products: 'Produkty',
  warehouses: 'Magazyny i lokalizacje',
  pallets: 'Palety LE',
  receipts: 'Przyjęcia towaru',
  orders: 'Zamówienia i kompletacja',
  shipments: 'Wysyłki',
  users: 'Użytkownicy',
}

const quickLinks: {
  id: PageId
  icon: string
  description: string
}[] = [
  {
    id: 'products',
    icon: '◈',
    description: 'Katalog artykułów i ich podstawowe dane.',
  },
  {
    id: 'warehouses',
    icon: '⌂',
    description: 'Magazyny, strefy i miejsca składowania.',
  },
  {
    id: 'pallets',
    icon: '▣',
    description: 'Jednostki logistyczne i stan palet.',
  },
  {
    id: 'receipts',
    icon: '↙',
    description: 'Rejestrowanie przyjęć magazynowych.',
  },
  {
    id: 'orders',
    icon: '☷',
    description: 'Zamówienia i proces kompletacji.',
  },
  {
    id: 'shipments',
    icon: '↗',
    description: 'Przygotowanie i potwierdzanie wysyłek.',
  },
]

function Dashboard({
  user,
  onNavigate,
}: {
  user: CurrentUser
  onNavigate: (page: PageId) => void
}) {
  return (
    <>
      <div className="welcome-section">
        <div>
          <span className="eyebrow">
            PANEL MAGAZYNOWY
          </span>

          <h1>Witaj, {user.fullName}!</h1>

          <p>
            Wybierz moduł, aby rozpocząć pracę z systemem.
          </p>
        </div>

        <div className="connection-chip">
          <span className="connection-dot" />
          Sesja aktywna
        </div>
      </div>

      <div className="summary-grid">
        <article className="summary-card">
          <span>Użytkownik</span>
          <strong>{user.fullName}</strong>
          <small>{user.email}</small>
        </article>

        <article className="summary-card">
          <span>Twoje role</span>
          <strong>
            {user.roles.join(', ') || 'Brak ról'}
          </strong>
          <small>Role pobrane z ASP.NET Core API</small>
        </article>

        <article className="summary-card">
          <span>Uwierzytelnianie</span>
          <strong>JWT</strong>
          <small>
            Token zweryfikowany przy logowaniu
          </small>
        </article>
      </div>

      <div className="section-heading">
        <h2>Moduły systemu</h2>
        <p>
          Przejdź do wybranego obszaru magazynu.
        </p>
      </div>

      <div className="module-grid">
        {quickLinks.map(item => (
          <button
            type="button"
            key={item.id}
            className="module-card"
            onClick={() => onNavigate(item.id)}
          >
            <span
              className="module-icon"
              aria-hidden="true"
            >
              {item.icon}
            </span>

            <span className="module-title">
              {pageTitles[item.id]}
            </span>

            <span className="module-description">
              {item.description}
            </span>

            <span className="module-action">
              Otwórz moduł →
            </span>
          </button>
        ))}
      </div>
    </>
  )
}

function ModulePlaceholder({
  title,
  onBack,
}: {
  title: string
  onBack: () => void
}) {
  return (
    <section className="panel placeholder-panel">
      <div
        className="placeholder-icon"
        aria-hidden="true"
      >
        ◇
      </div>

      <h2>{title}</h2>

      <p>
        Interfejs webowy tego modułu powstanie
        w kolejnych etapach.
      </p>

      <button
        type="button"
        className="button-primary"
        onClick={onBack}
      >
        Wróć do pulpitu
      </button>
    </section>
  )
}

function App() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [session, setSession] = useState<Session | null>(
    null
  )
  const [activePage, setActivePage] =
    useState<PageId>('dashboard')

  const [mobileMenuOpen, setMobileMenuOpen] =
    useState(false)

  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleLogout = useCallback(() => {
    setSession(null)
    setPassword('')
    setError(null)
    setActivePage('dashboard')
    setMobileMenuOpen(false)
  }, [])

  const handleUnauthorized = useCallback(() => {
    setSession(null)
    setPassword('')
    setActivePage('dashboard')
    setMobileMenuOpen(false)
    setError('Sesja wygasła. Zaloguj się ponownie.')
  }, [])

  function navigateTo(page: PageId) {
    setActivePage(page)
    setMobileMenuOpen(false)
  }

  async function handleLogin(
    event: FormEvent<HTMLFormElement>
  ) {
    event.preventDefault()

    setError(null)
    setIsLoading(true)

    try {
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          email: email.trim(),
          password,
        }),
      })

      if (response.status === 401) {
        throw new Error(
          'Nieprawidłowy e-mail lub hasło.'
        )
      }

      if (!response.ok) {
        throw new Error(
          `Błąd logowania (HTTP ${response.status}).`
        )
      }

      const login =
        (await response.json()) as LoginResponse

      if (!login.accessToken) {
        throw new Error(
          'Brak tokenu JWT w odpowiedzi serwera.'
        )
      }

      const meResponse = await fetch('/api/auth/me', {
        headers: {
          Authorization: `Bearer ${login.accessToken}`,
        },
      })

      if (!meResponse.ok) {
        throw new Error(
          'Nie udało się zweryfikować użytkownika.'
        )
      }

      const user =
        (await meResponse.json()) as CurrentUser

      setSession({
        token: login.accessToken,
        user,
      })

      setPassword('')
      setActivePage('dashboard')
    } catch (err) {
      setError(
        err instanceof Error
          ? err.message
          : 'Nieznany błąd logowania.'
      )
    } finally {
      setIsLoading(false)
    }
  }

  if (!session) {
    return (
      <div className="login-page">
        <main className="login-card">
          <div className="login-brand">
            <div className="brand-icon">W</div>
            <span>WAREHOUSE SYSTEM</span>
          </div>

          <h1>Logowanie</h1>

          <p className="subtitle">
            Zaloguj się do systemu zarządzania magazynem.
          </p>

          <form onSubmit={handleLogin}>
            <div className="field">
              <label htmlFor="email">
                Adres e-mail
              </label>

              <input
                id="email"
                type="email"
                autoComplete="username"
                value={email}
                onChange={event =>
                  setEmail(event.target.value)
                }
                required
                disabled={isLoading}
                placeholder="E-mail użytkownika"
              />
            </div>

            <div className="field">
              <label htmlFor="password">
                Hasło
              </label>

              <input
                id="password"
                type="password"
                autoComplete="current-password"
                value={password}
                onChange={event =>
                  setPassword(event.target.value)
                }
                required
                disabled={isLoading}
                placeholder="Wprowadź hasło"
              />
            </div>

            {error && (
              <div
                className="error-message"
                role="alert"
              >
                {error}
              </div>
            )}

            <button
              type="submit"
              className="button-primary login-submit"
              disabled={isLoading}
            >
              {isLoading
                ? 'Logowanie...'
                : 'Zaloguj się'}
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

  const user = session.user

  const isAdministrator =
    user.roles.includes('Administrator')

  const canEditStructure =
    isAdministrator || user.roles.includes('Manager')

  const canOperateLogistics =
    isAdministrator || user.roles.includes('WarehouseWorker')

  return (
    <div className="app-shell">
      {mobileMenuOpen && (
        <button
          type="button"
          className="sidebar-backdrop"
          onClick={() => setMobileMenuOpen(false)}
          aria-label="Zamknij menu"
        />
      )}

      <aside
        className={
          `sidebar ${mobileMenuOpen ? 'sidebar-open' : ''}`
        }
        id="main-navigation"
        aria-label="Nawigacja magazynowa"
      >
        <div className="sidebar-brand">
          <div className="brand-icon">W</div>

          <div>
            <strong>Warehouse</strong>
            <small>Management System</small>
          </div>
        </div>

        <nav>
          {navigation.map(group => (
            <div
              className="nav-group"
              key={group.title}
            >
              <div className="nav-group-label">
                {group.title}
              </div>

              {group.items
                .filter(
                  item =>
                    !item.adminOnly || isAdministrator
                )
                .map(item => (
                  <button
                    type="button"
                    key={item.id}
                    className={
                      activePage === item.id
                        ? 'nav-link nav-link-active'
                        : 'nav-link'
                    }
                    aria-current={
                      activePage === item.id
                        ? 'page'
                        : undefined
                    }
                    onClick={() =>
                      navigateTo(item.id)
                    }
                  >
                    <span
                      className="nav-icon"
                      aria-hidden="true"
                    >
                      {item.icon}
                    </span>

                    <span>{item.label}</span>
                  </button>
                ))}
            </div>
          ))}
        </nav>

        <div className="sidebar-footer">
          <span className="connection-dot" />
          System lokalny
        </div>
      </aside>

      <div className="app-workspace">
        <header className="topbar">
          <div className="topbar-left">
            <button
              type="button"
              className="menu-toggle"
              aria-label="Otwórz menu"
              aria-controls="main-navigation"
              aria-expanded={mobileMenuOpen}
              onClick={() =>
                setMobileMenuOpen(current => !current)
              }
            >
              ☰
            </button>

            <div>
              <small>
                Warehouse Management System
              </small>
              <strong>
                {pageTitles[activePage]}
              </strong>
            </div>
          </div>

          <div className="topbar-right">
            <div className="topbar-user">
              <span className="user-avatar">
                {user.fullName.charAt(0).toUpperCase()}
              </span>

              <div>
                <strong>{user.fullName}</strong>
                <small>
                  {user.roles.join(', ') || 'Użytkownik'}
                </small>
              </div>
            </div>

            <button
              type="button"
              className="button-secondary logout-button"
              onClick={handleLogout}
            >
              Wyloguj
            </button>
          </div>
        </header>

        <main className="page-content">
          {activePage === 'dashboard' ? (
            <Dashboard
              user={user}
              onNavigate={navigateTo}
            />
          ) : activePage === 'products' ? (
            <ProductsPage
              token={session.token}
              canEditProducts={canEditStructure}
              onUnauthorized={handleUnauthorized}
            />
          ) : activePage === 'warehouses' ? (
            <WarehousesPage
              token={session.token}
              canEditWarehouses={canEditStructure}
              onUnauthorized={handleUnauthorized}
            />
          ) : activePage === 'pallets' ? (
            <PalletsPage
              token={session.token}
              canCreatePallets={canOperateLogistics}
              onUnauthorized={handleUnauthorized}
            />
          ) : (
            <ModulePlaceholder
              title={pageTitles[activePage]}
              onBack={() => navigateTo('dashboard')}
            />
          )}
        </main>
      </div>
    </div>
  )
}

export default App
