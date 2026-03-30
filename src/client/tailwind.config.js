/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    "./src/**/*.{html,ts}",
  ],
  theme: {
    extend: {
      colors: {
        primary: '#eebd2b',
        'honey-amber': '#eebd2b',
        'mist-ivory': '#fdfbf7',
        'mist-cream': '#f8f5f0',
        'mist-sand': '#e5e1da',
        'misty-gray': '#94a3b8',
        charcoal: '#181611',
        'background-light': '#ffffff',
        'background-dark': '#181611',
        'misty-blue': '#f1f5f9',
      },
      fontFamily: {
        sans: ['"Plus Jakarta Sans"', 'Manrope', 'sans-serif'],
        display: ['Manrope', 'sans-serif'],
      },
      animation: {
        'slow-drift': 'drift 20s ease-in-out infinite alternate',
        'slow-pulse': 'pulse-ring 8s ease-in-out infinite',
      },
      keyframes: {
        drift: {
          '0%': { transform: 'translate(0, 0) scale(1)' },
          '100%': { transform: 'translate(5%, 5%) scale(1.1)' },
        },
        pulseRing: {
          '0%, 100%': { transform: 'scale(1)', opacity: '0.3' },
          '50%': { transform: 'scale(1.05)', opacity: '0.1' },
        },
      },
    },
  },
  plugins: [],
};
