import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-logo',
  standalone: true,
  imports: [CommonModule],
  template: `
    <svg 
      [attr.width]="size" 
      [attr.height]="size" 
      viewBox="0 0 40 40" 
      fill="none" 
      xmlns="http://www.w3.org/2000/svg"
      class="inline-block"
    >
      <!-- Outer shield shape -->
      <path 
        d="M20 2L4 10V20C4 28.837 10.4 36.8 20 38C29.6 36.8 36 28.837 36 20V10L20 2Z" 
        [attr.fill]="gradientId" 
        stroke="url(#shieldStroke)" 
        stroke-width="1.5"
        stroke-linejoin="round"
      />
      <!-- Inner vault/lock icon -->
      <rect x="13" y="17" width="14" height="11" rx="2" [attr.fill]="innerFill" opacity="0.9"/>
      <path d="M16 17V14C16 11.7909 17.7909 10 20 10C22.2091 10 24 11.7909 24 14V17" 
            stroke="white" 
            stroke-width="2" 
            stroke-linecap="round"/>
      <circle cx="20" cy="22.5" r="2" fill="white"/>
      <path d="M20 24.5V26" stroke="white" stroke-width="2" stroke-linecap="round"/>
      <!-- Gradient definitions -->
      <defs>
        <linearGradient [id]="gradientId" x1="4" y1="2" x2="36" y2="38" gradientUnits="userSpaceOnUse">
          <stop stop-color="#eebd2b"/>
          <stop offset="1" stop-color="#d4a017"/>
        </linearGradient>
        <linearGradient id="shieldStroke" x1="4" y1="2" x2="36" y2="38" gradientUnits="userSpaceOnUse">
          <stop stop-color="#b8860b"/>
          <stop offset="1" stop-color="#d4a017"/>
        </linearGradient>
      </defs>
    </svg>
  `,
  styles: [`
    :host {
      display: inline-block;
      line-height: 0;
    }
  `]
})
export class LogoComponent {
  @Input() size: number = 40;
  @Input() variant: 'default' | 'light' | 'dark' = 'default';

  get gradientId(): string {
    return this.variant === 'light' ? 'gradLight' : this.variant === 'dark' ? 'gradDark' : 'gradDefault';
  }

  get innerFill(): string {
    return this.variant === 'dark' ? '#181611' : 'white';
  }
}
