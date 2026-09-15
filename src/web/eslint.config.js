import js from '@eslint/js';
import globals from 'globals';
import tseslint from 'typescript-eslint';
import reactHooks from 'eslint-plugin-react-hooks';

export default tseslint.config(
  { ignores: ['dist'] },
  {
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,

      // Prohibido inyectar HTML: la CSP del channel-api no permite inline, y esta
      // regla cierra la puerta antes de que alguien lo intente (plan secc. 6.3).
      'react/no-danger': 'off',
      'no-restricted-syntax': [
        'error',
        {
          selector: 'JSXAttribute[name.name="dangerouslySetInnerHTML"]',
          message: 'Prohibido: inyectar HTML es la via mas corta a un XSS.',
        },
        {
          selector: 'CallExpression[callee.name="eval"]',
          message: 'Prohibido: la CSP bloquea eval y no deberia hacer falta.',
        },
      ],
    },
  },
);
