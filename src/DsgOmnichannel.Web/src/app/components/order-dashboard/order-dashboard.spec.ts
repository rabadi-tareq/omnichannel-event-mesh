import { ComponentFixture, TestBed } from '@angular/core/testing';

import { OrderDashboard } from './order-dashboard';

describe('OrderDashboard', () => {
  let component: OrderDashboard;
  let fixture: ComponentFixture<OrderDashboard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrderDashboard],
    }).compileComponents();

    fixture = TestBed.createComponent(OrderDashboard);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
