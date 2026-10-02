import type {
  CreateExpenseDto,
  ExpenseResponseDto,
  UpdateBudgetRequest,
  UpdateExpenseStatusDto,
} from './generated/types.gen';

export type Expense = {
  [Field in keyof Required<ExpenseResponseDto>]: NonNullable<ExpenseResponseDto[Field]>;
};

export type { CreateExpenseDto, UpdateBudgetRequest, UpdateExpenseStatusDto };
