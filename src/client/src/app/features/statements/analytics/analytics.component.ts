import { Component, Input, OnChanges, ElementRef, ViewChild, AfterViewInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { StatementDetail } from '../../../core/services/rewards.service';
import Chart from 'chart.js/auto';

interface SpendCategory {
  name: string;
  amount: number;
  percentage: number;
  count: number;
  color: string;
}

interface Merchant {
  name: string;
  amount: number;
  initials: string;
}

@Component({
  selector: 'app-statement-analytics',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './analytics.component.html',
  styleUrls: ['./analytics.component.css']
})
export class StatementAnalyticsComponent implements OnChanges, AfterViewInit, OnDestroy {
  @Input() statement!: StatementDetail;

  @ViewChild('categoryChart') categoryChartRef!: ElementRef<HTMLCanvasElement>;
  private chartInstance: Chart | null = null;

  categories: SpendCategory[] = [];
  topMerchants: Merchant[] = [];
  topCategoryName = '';
  topCategoryPercentage = '0%';

  ngOnChanges(): void {
    if (this.statement) {
      this.calculateAnalytics();
      this.updateChart();
    }
  }

  ngAfterViewInit(): void {
    this.updateChart();
  }

  ngOnDestroy(): void {
    if (this.chartInstance) {
      this.chartInstance.destroy();
    }
  }

  private calculateAnalytics(): void {
    const txns = this.statement.transactions || [];
    const debits = txns.filter(t => (t.type === 'DEBIT' || t.amount > 0) && !t.description.toLowerCase().includes('payment'));

    // Merchants
    const merchantMap = new Map<string, number>();
    for (const t of debits) {
      const desc = this.cleanMerchantName(t.description);
      const amt = Math.abs(t.amount);
      merchantMap.set(desc, (merchantMap.get(desc) || 0) + amt);
    }
    
    this.topMerchants = Array.from(merchantMap.entries())
      .map(([name, amount]) => ({
        name,
        amount,
        initials: name.substring(0, 2).toUpperCase()
      }))
      .sort((a, b) => b.amount - a.amount)
      .slice(0, 6);

    // Categories
    const catMap = new Map<string, { amount: number; count: number }>();
    for (const t of debits) {
      const cat = this.guessCategory(t.description);
      const amt = Math.abs(t.amount);
      const current = catMap.get(cat) || { amount: 0, count: 0 };
      catMap.set(cat, { amount: current.amount + amt, count: current.count + 1 });
    }

    let total = 0;
    const catArray = Array.from(catMap.entries());
    catArray.forEach(([_, data]) => total += data.amount);

    const colors = ['#8a5100', '#b86d1a', '#d19a4b', '#6f7f91', '#95a3b2', '#d6c7b2'];
    
    this.categories = catArray
      .map(([name, data], idx) => ({
        name,
        amount: data.amount,
        count: data.count,
        percentage: total > 0 ? (data.amount / total) * 100 : 0,
        color: colors[idx % colors.length]
      }))
      .sort((a, b) => b.amount - a.amount);

    if (this.categories.length > 0) {
      this.topCategoryName = this.categories[0].name;
      this.topCategoryPercentage = `${this.categories[0].percentage.toFixed(1)}%`;
    } else {
      this.topCategoryName = 'Others';
      this.topCategoryPercentage = '0.0%';
    }
  }

  private updateChart(): void {
    if (!this.categoryChartRef?.nativeElement || this.categories.length === 0) return;

    if (this.chartInstance) {
      this.chartInstance.destroy();
    }

    const ctx = this.categoryChartRef.nativeElement.getContext('2d');
    if (!ctx) return;

    const data = this.categories.map(c => c.amount);
    const bgColors = this.categories.map(c => c.color);
    
    // total inner sum for center (or just total billed)
    const totalAmount = data.reduce((a, b) => a + b, 0).toLocaleString('en-IN', { style: 'currency', currency: 'INR' });

    this.chartInstance = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: this.categories.map(c => c.name),
        datasets: [{
          data,
          backgroundColor: bgColors,
          borderWidth: 0,
          hoverOffset: 4
        }]
      },
      options: {
        cutout: '75%',
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: { enabled: false }
        }
      },
      plugins: [{
        id: 'centerText',
        beforeDraw: function(chart) {
          const w = chart.width;
          const h = chart.height;
          const ctx = chart.ctx;
          
          ctx.save();
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.font = '700 16px "Segoe UI", sans-serif';
          ctx.fillStyle = '#1c1917';
          ctx.fillText(totalAmount, w / 2, h / 2);
          ctx.restore();
        }
      }]
    });
  }

  private guessCategory(desc: string): string {
    const d = desc.toLowerCase();
    if (d.includes('food') || d.includes('rest') || d.includes('dine') || d.includes('cafe') || d.includes('zomato') || d.includes('swiggy')) return 'Dining';
    if (d.includes('shop') || d.includes('amazon') || d.includes('flipkart') || d.includes('myntra') || d.includes('store') || d.includes('market') || d.includes('mall')) return 'Shopping';
    if (d.includes('travel') || d.includes('uber') || d.includes('ola') || d.includes('flight') || d.includes('hotel') || d.includes('lyft') || d.includes('transport') || d.includes('metro') || d.includes('fuel') || d.includes('petrol')) return 'Travel';
    if (d.includes('rent') || d.includes('mortgage') || d.includes('lease') || d.includes('housing')) return 'Housing';
    if (d.includes('electricity') || d.includes('water') || d.includes('internet') || d.includes('wifi') || d.includes('broadband') || d.includes('utility') || d.includes('airtel') || d.includes('jio') || d.includes('recharge')) return 'Utilities';
    if (d.includes('netflix') || d.includes('prime') || d.includes('spotify') || d.includes('hotstar') || d.includes('movie') || d.includes('game')) return 'Entertainment';
    return 'Others';
  }

  private cleanMerchantName(desc: string): string {
    let clean = desc.replace(/[0-9*#/-]/g, ' ').replace(/\s+/g, ' ').trim();
    if (clean.length > 24) clean = clean.substring(0, 24);
    if (!clean) return 'Unknown';

    return clean.split(' ').map(w => w.charAt(0).toUpperCase() + w.slice(1).toLowerCase()).join(' ');
  }

  totalSpend(): number {
    return this.categories.reduce((sum, c) => sum + c.amount, 0);
  }

  totalTransactions(): number {
    return this.categories.reduce((sum, c) => sum + c.count, 0);
  }
}
