import { Component } from '@angular/core';
import { OrderDashboardComponent } from './components/order-dashboard/order-dashboard';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [OrderDashboardComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  title = 'DsgOmnichannel.Web';
}
