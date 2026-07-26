import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./components/order-dashboard/order-dashboard').then(m => m.OrderDashboardComponent)
  },
  {
    path: 'order-journey',
    loadComponent: () =>
      import('./pages/order-journey/order-journey').then(m => m.OrderJourneyComponent)
  }
];
