// import { Providers } from './providers';
// import { DashboardPage } from '@/pages/dashboard/DashboardPage';
// import { NewcomersWidget } from '@/widgets/newcomers';
//
// export default function App() {
//     return (
//         <Providers>
//             <DashboardPage />
//             <NewcomersWidget /> {/* floating top-right */}
//         </Providers>
//     );
// }

import { Providers } from './providers';
import { AppHeader } from './AppHeader';
import { MainPage } from '@/pages/main/MainPage';
import { NewcomersWidget } from '@/widgets/newcomers';

export default function App() {
    return (
        <Providers>
            <div className="min-h-screen bg-slate-50">
                <AppHeader />
                <MainPage />
                <NewcomersWidget /> {/* persistent floating indicator */}
            </div>
        </Providers>
    );
}