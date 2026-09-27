from patchlib import patch

patch(r"src\web\src\App.tsx", [
("""import { InsightsPage } from './routes/InsightsPage';
""",
"""import { InsightsPage } from './routes/InsightsPage';
import { CodePage } from './routes/CodePage';
"""),
("""const FILTERED_ROUTES = new Set(['/', '/portfolio', '/insights', '/roadmap']);""",
"""const FILTERED_ROUTES = new Set(['/', '/portfolio', '/insights', '/code', '/roadmap']);"""),
("""        <Route path="insights" element={<InsightsPage />} />
""",
"""        <Route path="insights" element={<InsightsPage />} />
        <Route path="code" element={<CodePage />} />
"""),
("""            <Tab to="/insights" search={search}>
              Insights
            </Tab>
""",
"""            <Tab to="/insights" search={search}>
              Insights
            </Tab>
            <Tab to="/code" search={search}>
              Code
            </Tab>
"""),
])
